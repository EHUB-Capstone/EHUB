using EHub.Contracts.Teams;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.StudentSelfService;

public interface IStudentClassSelfServiceHandler
{
    Task<Result<MyClassesResponse>> GetMyClassesAsync(Guid userId, string role, string scope = "Current", CancellationToken cancellationToken = default);
    Task<Result<StudentClassDetailResponse>> GetClassDetailAsync(Guid classId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<StudentClassDetailResponse>> GetClassDetailByIdentifierAsync(string classIdentifier, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<MyTeamResponse>> GetMyTeamAsync(Guid userId, string role, CancellationToken cancellationToken = default);
}