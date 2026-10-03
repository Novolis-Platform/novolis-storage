using System.Linq.Expressions;
using System.Reflection;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Novolis.Storage.Abstractions;
using Novolis.Storage.AzureTables;
using Novolis.Storage.InMemory;

namespace Novolis.Storage.Unit.Repositories;

public sealed class AzureTableQueryTests
{
    [Test]
    public async Task Query_requires_an_azure_table_repository()
    {
        await Assert.That(() => ((IRepository<AzureTableRecord>)null!).Query()).Throws<ArgumentNullException>();

        var services = new ServiceCollection();
        services.AddStorage(b => b.AddInMemoryProvider());
        await using var provider = services.BuildServiceProvider();
        var memory = provider.GetRequiredService<IRepository<AzureTableRecord>>();
        await Assert.That(() => memory.Query()).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Where_select_and_take_translate_without_a_service_call()
    {
        var query = Repository().Query();
        var filtered = query.Where(e => e.Active).Where(e => e.Count >= 0);
        var plan = AzureTableQueryPlan.Parse(filtered.Expression);
        await Assert.That(plan.Predicates.Count).IsEqualTo(2);
        await Assert.That(plan.BuildFilter()).Contains(" and ");

        var open = AzureTableQueryPlan.Parse(query.Expression);
        await Assert.That(open.Predicates.Count).IsEqualTo(0);
        await Assert.That(open.BuildFilter()).IsEqualTo(AzureTableFilterTranslator.PartitionOnly());

        var selected = query.Select(e => e.Name);
        await Assert.That(AzureTableQueryPlan.Parse(selected.Expression).Columns).IsEquivalentTo(new[] { nameof(AzureTableRecord.Name) });
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => e.Id).Expression).Columns).IsNull();
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => e).Expression).Columns).IsNull();
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => (object)e.Name).Expression).Columns)
            .IsEquivalentTo(new[] { nameof(AzureTableRecord.Name) });
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => new { e.Name, e.Count }).Expression).Columns!.Order().ToArray())
            .IsEquivalentTo(new[] { nameof(AzureTableRecord.Count), nameof(AzureTableRecord.Name) });
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => new AzureTableNameView { Name = e.Name, Count = e.Count }).Expression).Columns!.Count)
            .IsEqualTo(2);
        await Assert.That(AzureTableQueryPlan.Parse(query.Select(e => new AzureTableNameView()).Expression).Columns).IsNull();

        var taken = AzureTableQueryPlan.Parse(query.Where(e => e.Active).Take(2).Take(0).Expression);
        await Assert.That(taken.Take).IsEqualTo(0);

        var created = query.Provider.CreateQuery(filtered.Expression);
        await Assert.That(created.ElementType).IsEqualTo(typeof(AzureTableRecord));
        await Assert.That(() => query.Provider.Execute(query.Expression)).Throws<NotSupportedException>();
        await Assert.That(() => query.Provider.Execute<int>(query.Expression)).Throws<NotSupportedException>();
    }

    [Test]
    public async Task Unsupported_linq_is_rejected_before_enumeration()
    {
        var query = Repository().Query();
        await Assert.That(() => query.Where(e => e.Name.StartsWith("A"))).Throws<NotSupportedException>();
        await Assert.That(() => query.Where<AzureTableRecord>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableRepositoryExtensions.Where(null!, (AzureTableRecord e) => e.Active)).Throws<ArgumentNullException>();
        await Assert.That(() => query.Select(e => e.Name.Length)).Throws<NotSupportedException>();
        await Assert.That(() => query.Select(e => !e.Active)).Throws<NotSupportedException>();
        await Assert.That(() => query.Select(e => new { Entity = e })).Throws<NotSupportedException>();
        await Assert.That(() => query.Select<AzureTableRecord, string>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableRepositoryExtensions.Select<AzureTableRecord, string>(null!, e => e.Name)).Throws<ArgumentNullException>();
        await Assert.That(() => query.OrderBy(e => e.Name)).Throws<NotSupportedException>();
        await Assert.That(() => query.OrderBy<AzureTableRecord, string>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableRepositoryExtensions.OrderBy<AzureTableRecord, string>(null!, e => e.Name)).Throws<ArgumentNullException>();
        await Assert.That(() => query.OrderByDescending(e => e.Name)).Throws<NotSupportedException>();
        await Assert.That(() => query.OrderByDescending<AzureTableRecord, string>(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => AzureTableRepositoryExtensions.OrderByDescending<AzureTableRecord, string>(null!, e => e.Name)).Throws<ArgumentNullException>();
        await Assert.That(() => query.Skip(1)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableRepositoryExtensions.Skip<AzureTableRecord>(null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => query.Take(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => AzureTableRepositoryExtensions.Take<AzureTableRecord>(null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => query.Select(e => e.Name).Where(name => name == "Ada")).Throws<NotSupportedException>();
        await Assert.That(() => query.Select(e => e.Name).Select(name => name)).Throws<NotSupportedException>();

        var where = QueryableMethod(nameof(Queryable.Where), parameters: 2, funcArity: 2);
        var closed = where.MakeGenericMethod(typeof(AzureTableRecord));
        var lambda = (Expression<Func<AzureTableRecord, bool>>)(e => e.Active);
        var unquoted = Expression.Call(closed, query.Expression, lambda);
        await Assert.That(AzureTableQueryPlan.Parse(unquoted).Predicates.Count).IsEqualTo(1);

        var quotedWrong = Expression.Call(closed, query.Expression, Expression.Constant(lambda, lambda.GetType()));
        await Assert.That(() => AzureTableQueryPlan.Parse(quotedWrong)).Throws<NotSupportedException>();

        var distinct = QueryableMethod(nameof(Queryable.Distinct), parameters: 1, funcArity: 0);
        var distinctCall = Expression.Call(distinct.MakeGenericMethod(typeof(AzureTableRecord)), query.Expression);
        await Assert.That(() => AzureTableQueryPlan.Parse(distinctCall)).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableQueryPlan.Parse(Expression.Constant(1))).Throws<NotSupportedException>();
        await Assert.That(() => AzureTableQueryPlan.Parse(Expression.Constant(new List<int>()))).Throws<NotSupportedException>();

        var take = QueryableMethod(nameof(Queryable.Take), parameters: 2, funcArity: 0, lastType: typeof(int));
        var badTake = Expression.Call(
            take.MakeGenericMethod(typeof(AzureTableRecord)),
            query.Expression,
            Expression.Convert(Expression.Constant(1), typeof(int)));
        await Assert.That(() => AzureTableQueryPlan.Parse(badTake)).Throws<NotSupportedException>();

        await Assert.That(() => AzureTableQueryPlan.Parse(SelectCall(query, MemberBindSelector()))).Throws<NotSupportedException>();
        await Assert.That(AzureTableQueryPlan.Parse(SelectCall(query, CheckedSelector())).Columns)
            .IsEquivalentTo(new[] { nameof(AzureTableRecord.Count) });
    }

    private static MethodInfo QueryableMethod(string name, int parameters, int funcArity, Type? lastType = null) =>
        typeof(Queryable).GetMethods().Single(method =>
            method.Name == name &&
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == parameters &&
            (lastType is null || method.GetParameters()[^1].ParameterType == lastType) &&
            (funcArity == 0
                ? method.GetParameters().All(parameter => !parameter.ParameterType.IsGenericType || parameter.ParameterType.GetGenericTypeDefinition() != typeof(Expression<>))
                : FuncArity(method.GetParameters()[^1].ParameterType) == funcArity));

    private static int FuncArity(Type type)
    {
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Expression<>))
            return 0;

        var func = type.GetGenericArguments()[0];
        return func.IsGenericType ? func.GetGenericArguments().Length : 0;
    }

    private static AzureTableRepository<AzureTableRecord> Repository() =>
        new(new TableServiceClient("UseDevelopmentStorage=true"), new AzureTableOptions { ConnectionString = "UseDevelopmentStorage=true" });

    private static MethodCallExpression SelectCall(IAzureTableQuery<AzureTableRecord> query, LambdaExpression selector)
    {
        var method = QueryableMethod(nameof(Queryable.Select), parameters: 2, funcArity: 2);
        var closed = method.MakeGenericMethod(typeof(AzureTableRecord), selector.ReturnType);
        return Expression.Call(closed, query.Expression, Expression.Quote(selector));
    }

    private static LambdaExpression MemberBindSelector()
    {
        var parameter = Expression.Parameter(typeof(AzureTableRecord), "x");
        var name = typeof(AzureTableNode).GetProperty(nameof(AzureTableNode.Name))!;
        var child = typeof(AzureTableNode).GetProperty(nameof(AzureTableNode.Child))!;
        var init = Expression.MemberInit(
            Expression.New(typeof(AzureTableNode)),
            Expression.MemberBind(child, Expression.Bind(name, Expression.Property(parameter, nameof(AzureTableRecord.Name)))));
        return Expression.Lambda(init, parameter);
    }

    private static LambdaExpression CheckedSelector()
    {
        var parameter = Expression.Parameter(typeof(AzureTableRecord), "x");
        var body = Expression.ConvertChecked(Expression.Property(parameter, nameof(AzureTableRecord.Count)), typeof(long));
        return Expression.Lambda(body, parameter);
    }
}
