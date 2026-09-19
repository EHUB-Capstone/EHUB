using System.Collections.Generic;
using System.Linq;
using EHub.Domain.Entities;

namespace EHub.Application.Features.Classes.ExportAdminClassData;

internal static class AdminClassExportOrdering
{
    internal static IReadOnlyCollection<Class> Apply(IEnumerable<Class> classes) => classes
        .OrderBy(@class => @class.Course.Code, NaturalCodeComparer.Instance)
        .ThenBy(@class => @class.ClassIndex)
        .ThenBy(@class => @class.ClassCode, NaturalCodeComparer.Instance)
        .ThenBy(@class => @class.Id)
        .ToArray();
}
