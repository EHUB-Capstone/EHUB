using EHub.Contracts.Mentoring;
using EHub.Shared.Results;

namespace EHub.Application.Features.Mentoring;

public interface IMentoringSessionHandler
{
    Task<Result<IReadOnlyCollection<MentoringSessionResponse>>> ListAsync(Guid? teamId, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringSessionResponse>> CreateAsync(SaveMentoringSessionRequest request, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringSessionResponse>> UpdateAsync(Guid id, SaveMentoringSessionRequest request, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringSessionResponse>> CompleteAsync(Guid id, SaveMentoringNotesRequest request, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringSessionResponse>> CancelAsync(Guid id, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringActionItemResponse>> AddActionItemAsync(Guid id, CreateMentoringActionItemRequest request, Guid userId, string role, CancellationToken ct);
    Task<Result<MentoringFeedbackResponse>> SaveFeedbackAsync(Guid id, SaveMentoringFeedbackRequest request, Guid userId, string role, CancellationToken ct);
    Task<Result<IReadOnlyCollection<MentoringFeedbackResponse>>> GetFeedbackAsync(Guid id, Guid userId, string role, CancellationToken ct);
}
