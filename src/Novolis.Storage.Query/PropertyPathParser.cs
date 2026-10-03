using System.Linq.Expressions;
using System.Reflection;

namespace Novolis.Storage.Query;

internal static class PropertyPathParser
{
    public static PropertyPath Parse<T, TValue>(Expression<Func<T, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var segments = new List<string>();
        var current = Unwrap(selector.Body);
        while (current is MemberExpression member)
        {
            if (member.Member is not PropertyInfo property || property.GetIndexParameters().Length != 0)
                throw Reject("Only ordinary properties may be used as query selectors.");

            if (member.Expression is null)
                throw Reject("Static properties cannot be query selectors.");

            segments.Add(property.Name);
            current = Unwrap(member.Expression);
        }

        if (current != selector.Parameters[0] || segments.Count == 0)
            throw Reject("Query selectors must be a property path rooted at the entity parameter.");

        segments.Reverse();
        return new PropertyPath(segments);
    }

    private static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;
        return expression;
    }

    private static QueryNotSupportedException Reject(string message) =>
        new(message + " Client-side evaluation is not performed.");
}
