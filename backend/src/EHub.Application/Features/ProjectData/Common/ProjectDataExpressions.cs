using System.Linq.Expressions;

namespace EHub.Application.Features.ProjectData.Common;

internal static class ProjectDataExpressions
{
    /// <summary>Combines two predicates over the same parameter with a logical OR, keeping it translatable by EF.</summary>
    internal static Expression<Func<T, bool>> Or<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(Expression.OrElse(left.Body, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
