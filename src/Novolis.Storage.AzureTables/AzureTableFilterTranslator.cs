using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Azure.Data.Tables;

namespace Novolis.Storage.AzureTables;

/// <summary>
/// Translates a predicate into an Azure Table OData filter.
/// Values go through <see cref="TableClient.CreateQueryFilter(FormattableString)"/> so quotes,
/// booleans, numbers, dates, and guids match the service. Row keys are strings, never <c>guid'...'</c>.
/// </summary>
internal static class AzureTableFilterTranslator
{
    public static string PartitionOnly() =>
        TableClient.CreateQueryFilter($"PartitionKey eq {AzureTableKeys.Partition}");

    public static string WithPartition<T>(Expression<Func<T, bool>> predicate) =>
        $"({PartitionOnly()}) and ({Translate(predicate)})";

    public static string Translate<T>(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return Visit(predicate.Body);
    }

    private static string Visit(Expression node)
    {
        node = Unwrap(node);
        switch (node)
        {
            case BinaryExpression binary when binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse or ExpressionType.And or ExpressionType.Or:
                var keyword = binary.NodeType is ExpressionType.AndAlso or ExpressionType.And ? "and" : "or";
                return $"({Visit(binary.Left)}) {keyword} ({Visit(binary.Right)})";
            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                return $"not ({Visit(unary.Operand)})";
            case BinaryExpression binary when IsComparison(binary.NodeType):
                return Comparison(binary);
            case MemberExpression member when IsBoolMember(member):
                return Format(member, "eq", true);
            case MethodCallExpression call:
                if (TryEquals(call, out var equalsMember, out var equalsValue))
                    return Format(equalsMember, "eq", Evaluate(equalsValue));
                throw new NotSupportedException(
                    $"Azure Table Storage has no server-side '{call.Method.Name}' operator. The query was rejected so it is not applied to a partial page of results.");
            default:
                throw new NotSupportedException(
                    $"Azure Table Storage cannot evaluate this query server-side ({node.NodeType}). Supported filters are comparisons, and, or, and not.");
        }
    }

    private static string Comparison(BinaryExpression binary)
    {
        if (!TrySplit(binary, out var member, out var valueExpression, out var flipped))
            throw new NotSupportedException("Azure Table filters must compare a stored property to a value.");

        return Format(member, Operator(binary.NodeType, flipped), Evaluate(valueExpression));
    }

    private static string Format(MemberExpression member, string op, object? value)
    {
        if (member.Member is not PropertyInfo property)
            throw new NotSupportedException("Azure Table queries can only filter on properties.");

        if (value is null)
            throw new NotSupportedException("Azure Table Storage does not support null comparisons.");

        if (property.Name == nameof(Abstractions.IHasId.Id))
        {
            var rowKey = value switch
            {
                Guid id => AzureTableKeys.RowKey(id),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
                    ?? throw new NotSupportedException("Id comparisons must be a Guid or string."),
            };
            return FormatColumn("RowKey", op, rowKey);
        }

        if (property.Name is "PartitionKey" or "RowKey" or "Timestamp" or "ETag")
            throw new NotSupportedException($"Property '{property.Name}' conflicts with an Azure Table system property.");

        return FormatColumn(property.Name, op, AzureTableValues.Normalize(property.PropertyType, value));
    }

    private static string FormatColumn(string column, string op, object value) =>
        TableClient.CreateQueryFilter(FormattableStringFactory.Create(column + " " + op + " {0}", value));

    private static bool TrySplit(BinaryExpression binary, out MemberExpression member, out Expression value, out bool flipped)
    {
        if (Unwrap(binary.Left) is MemberExpression left && IsEntityMember(left))
        {
            member = left;
            value = binary.Right;
            flipped = false;
            return true;
        }

        if (Unwrap(binary.Right) is MemberExpression right && IsEntityMember(right))
        {
            member = right;
            value = binary.Left;
            flipped = true;
            return true;
        }

        member = null!;
        value = null!;
        flipped = false;
        return false;
    }

    private static bool TryEquals(MethodCallExpression call, out MemberExpression member, out Expression value)
    {
        member = null!;
        value = null!;
        if (!string.Equals(call.Method.Name, "Equals", StringComparison.Ordinal))
            return false;

        if (call.Object is not null && call.Arguments.Count == 1 && Unwrap(call.Object) is MemberExpression instance && IsEntityMember(instance))
        {
            member = instance;
            value = call.Arguments[0];
            return true;
        }

        if (call.Object is null && call.Arguments.Count == 2)
        {
            if (Unwrap(call.Arguments[0]) is MemberExpression left && IsEntityMember(left))
            {
                member = left;
                value = call.Arguments[1];
                return true;
            }

            if (Unwrap(call.Arguments[1]) is MemberExpression right && IsEntityMember(right))
            {
                member = right;
                value = call.Arguments[0];
                return true;
            }
        }

        return false;
    }

    private static bool IsEntityMember(MemberExpression member)
    {
        var current = member.Expression;
        if (current is null)
            return false;

        current = Unwrap(current);
        return current is ParameterExpression;
    }

    private static bool IsBoolMember(MemberExpression member)
    {
        if (!IsEntityMember(member) || member.Member is not PropertyInfo property)
            return false;

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return type == typeof(bool);
    }

    private static object? Evaluate(Expression expression)
    {
        expression = Unwrap(expression);
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)));
        return lambda.Compile()();
    }

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        return expression;
    }

    private static bool IsComparison(ExpressionType type) => type is
        ExpressionType.Equal or
        ExpressionType.NotEqual or
        ExpressionType.GreaterThan or
        ExpressionType.GreaterThanOrEqual or
        ExpressionType.LessThan or
        ExpressionType.LessThanOrEqual;

    private static string Operator(ExpressionType type, bool flipped) => (type, flipped) switch
    {
        (ExpressionType.Equal, _) => "eq",
        (ExpressionType.NotEqual, _) => "ne",
        (ExpressionType.GreaterThan, false) => "gt",
        (ExpressionType.GreaterThan, true) => "lt",
        (ExpressionType.GreaterThanOrEqual, false) => "ge",
        (ExpressionType.GreaterThanOrEqual, true) => "le",
        (ExpressionType.LessThan, false) => "lt",
        (ExpressionType.LessThan, true) => "gt",
        (ExpressionType.LessThanOrEqual, false) => "le",
        (ExpressionType.LessThanOrEqual, true) => "ge",
        _ => throw new NotSupportedException($"Azure Table Storage cannot evaluate operator {type}."),
    };
}
