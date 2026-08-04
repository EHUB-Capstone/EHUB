using EHub.Contracts.Subjects;
using EHub.Shared.Results;

namespace EHub.Application.Features.Subjects.Roadmap;

public interface ISubjectRoadmapHandler
{
    Task<Result<RoadmapItemResponse>> CreateAsync(string subjectCode, SaveRoadmapItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<RoadmapItemResponse>> UpdateAsync(string subjectCode, Guid itemId, SaveRoadmapItemRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string subjectCode, Guid itemId, CancellationToken cancellationToken = default);
}
