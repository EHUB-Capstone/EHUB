using System.Linq;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Classes.GetClasses;

internal static class ClassListOrdering
{
    internal static IOrderedQueryable<Class> Apply(IQueryable<Class> query, string normalizedSort)
    {
        return normalizedSort switch
        {
            "-code" or "-classcode" => query
                .OrderByDescending(@class => @class.Course.Code)
                .ThenByDescending(@class => @class.ClassIndex)
                .ThenByDescending(@class => @class.ClassCode)
                .ThenByDescending(@class => @class.Id),
            "createdat" => query
                .OrderBy(@class => @class.CreatedAt)
                .ThenBy(@class => @class.Id),
            "-createdat" => query
                .OrderByDescending(@class => @class.CreatedAt)
                .ThenByDescending(@class => @class.Id),
            "classindex" => query
                .OrderBy(@class => @class.ClassIndex)
                .ThenBy(@class => @class.Course.Code)
                .ThenBy(@class => @class.ClassCode)
                .ThenBy(@class => @class.Id),
            "-classindex" => query
                .OrderByDescending(@class => @class.ClassIndex)
                .ThenBy(@class => @class.Course.Code)
                .ThenBy(@class => @class.ClassCode)
                .ThenBy(@class => @class.Id),
            _ => query
                .OrderBy(@class => @class.Course.Code)
                .ThenBy(@class => @class.ClassIndex)
                .ThenBy(@class => @class.ClassCode)
                .ThenBy(@class => @class.Id)
        };
    }
}
