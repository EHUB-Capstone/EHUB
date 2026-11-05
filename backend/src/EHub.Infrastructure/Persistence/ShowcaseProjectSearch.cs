using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EHub.Infrastructure.Persistence;

public sealed class ShowcaseProjectSearch : IShowcaseProjectSearch
{
    public IQueryable<Project> Apply(IQueryable<Project> projects, string search)
    {
        // Search input is literal text, not a user-supplied LIKE pattern.
        var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return projects.Where(project => EF.Functions.ILike(project.Name, pattern, "\\") ||
            project.Description != null && EF.Functions.ILike(project.Description, pattern, "\\"));
    }
}
