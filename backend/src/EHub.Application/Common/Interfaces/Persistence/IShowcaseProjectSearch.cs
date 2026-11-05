using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

/// <summary>Provider-specific case-insensitive substring search; the query remains server-side.</summary>
public interface IShowcaseProjectSearch
{
    IQueryable<Project> Apply(IQueryable<Project> projects, string search);
}
