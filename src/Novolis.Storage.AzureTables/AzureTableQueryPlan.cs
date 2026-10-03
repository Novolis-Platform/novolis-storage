using System.Linq.Expressions;
using System.Reflection;

namespace Novolis.Storage.AzureTables;

internal sealed class AzureTableQueryPlan
{
    private AzureTableQueryPlan(Type entityType, IReadOnlyList<LambdaExpression> predicates, LambdaExpression? selector, int? take)
    {
        EntityType = entityType;
        Predicates = predicates;
        Selector = selector;
        Take = take;
        Columns = selector is null ? null : ColumnsOf(selector);
    }

    public Type EntityType { get; }

    public IReadOnlyList<LambdaExpression> Predicates { get; }

    public LambdaExpression? Selector { get; }

    public IReadOnlyList<string>? Columns { get; }

    public int? Take { get; }

    public static AzureTableQueryPlan Parse(Expression expression)
    {
        var entityType = EntityTypeOf(expression);
        var predicates = new List<LambdaExpression>();
        LambdaExpression? selector = null;
        int? take = null;
        Walk(expression, entityType, predicates, ref selector, ref take);
        return new AzureTableQueryPlan(entityType, predicates, selector, take);
    }

    public string BuildFilter()
    {
        if (Predicates.Count == 0)
            return AzureTableFilterTranslator.PartitionOnly();

        var parameter = Expression.Parameter(EntityType, "entity");
        Expression? body = null;
        foreach (var predicate in Predicates)
        {
            var replaced = new ParameterReplacer(predicate.Parameters[0], parameter).Visit(predicate.Body)!;
            body = body is null ? replaced : Expression.AndAlso(body, replaced);
        }

        var delegateType = typeof(Func<,>).MakeGenericType(EntityType, typeof(bool));
        var lambda = Expression.Lambda(delegateType, body!, parameter);
        var method = typeof(AzureTableFilterTranslator)
            .GetMethod(nameof(AzureTableFilterTranslator.WithPartition))!
            .MakeGenericMethod(EntityType);
        return (string)method.Invoke(null, [lambda])!;
    }

    private static void Walk(
        Expression expression,
        Type entityType,
        List<LambdaExpression> predicates,
        ref LambdaExpression? selector,
        ref int? take)
    {
        if (expression is ConstantExpression constant && IsQueryType(constant.Type))
            return;

        if (expression is not MethodCallExpression call || call.Method.DeclaringType != typeof(Queryable))
            throw AzureTableFilterTranslator.Reject(expression.NodeType.ToString());

        switch (call.Method.Name)
        {
            case nameof(Queryable.Where):
                Walk(call.Arguments[0], entityType, predicates, ref selector, ref take);
                if (call.Method.GetGenericArguments()[0] != entityType)
                    throw AzureTableFilterTranslator.Reject("Where after Select");
                var predicate = AsLambda(call.Arguments[1]);
                ValidatePredicate(entityType, predicate);
                predicates.Add(predicate);
                return;
            case nameof(Queryable.Select):
                Walk(call.Arguments[0], entityType, predicates, ref selector, ref take);
                if (selector is not null)
                    throw AzureTableFilterTranslator.Reject("Select");
                selector = AsLambda(call.Arguments[1]);
                return;
            case nameof(Queryable.Take):
                Walk(call.Arguments[0], entityType, predicates, ref selector, ref take);
                if (call.Arguments[1] is not ConstantExpression { Value: int count })
                    throw AzureTableFilterTranslator.Reject(nameof(Queryable.Take));
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count));
                take = count;
                return;
            default:
                throw AzureTableFilterTranslator.Reject(call.Method.Name);
        }
    }

    private static Type EntityTypeOf(Expression expression)
    {
        var current = expression;
        while (current is MethodCallExpression call)
            current = call.Arguments[0];

        if (current is ConstantExpression constant && IsQueryType(constant.Type))
            return constant.Type.GenericTypeArguments[0];

        throw AzureTableFilterTranslator.Reject(expression.NodeType.ToString());
    }

    private static bool IsQueryType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AzureTableQuery<>);

    private static void ValidatePredicate(Type entityType, LambdaExpression predicate)
    {
        var method = typeof(AzureTableFilterTranslator)
            .GetMethod(nameof(AzureTableFilterTranslator.Translate))!
            .MakeGenericMethod(entityType);
        try
        {
            method.Invoke(null, [predicate]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is Exception inner)
        {
            throw inner;
        }
    }

    private static LambdaExpression AsLambda(Expression expression)
    {
        if (expression is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            expression = quote.Operand;

        return expression as LambdaExpression ?? throw AzureTableFilterTranslator.Reject("lambda");
    }

    private static IReadOnlyList<string>? ColumnsOf(LambdaExpression selector)
    {
        if (selector.Body is ParameterExpression)
            return null;

        var members = new HashSet<string>(StringComparer.Ordinal);
        Collect(selector.Body, selector.Parameters[0], members);
        members.Remove(nameof(Abstractions.IHasId.Id));
        return members.Count == 0 ? null : members.ToArray();
    }

    private static void Collect(Expression node, ParameterExpression parameter, HashSet<string> members)
    {
        switch (node)
        {
            case ParameterExpression:
                throw AzureTableFilterTranslator.Reject("entity projection");
            case MemberExpression member when member.Expression == parameter:
                members.Add(member.Member.Name);
                return;
            case MemberExpression:
                throw AzureTableFilterTranslator.Reject("member");
            case NewExpression newer:
                foreach (var argument in newer.Arguments)
                    Collect(argument, parameter, members);
                return;
            case MemberInitExpression init:
                Collect(init.NewExpression, parameter, members);
                foreach (var binding in init.Bindings)
                {
                    if (binding is not MemberAssignment assignment)
                        throw AzureTableFilterTranslator.Reject("member binding");
                    Collect(assignment.Expression, parameter, members);
                }

                return;
            case UnaryExpression unary when unary.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked:
                Collect(unary.Operand, parameter, members);
                return;
            default:
                throw AzureTableFilterTranslator.Reject(node.NodeType.ToString());
        }
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }
}
