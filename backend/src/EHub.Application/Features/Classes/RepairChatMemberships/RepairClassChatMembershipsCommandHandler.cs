using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EHub.Application.Features.Classes.RepairChatMemberships;

public sealed class RepairClassChatMembershipsCommandHandler : IRepairClassChatMembershipsCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClassChatMembershipSynchronizer _synchronizer;
    private readonly ILogger<RepairClassChatMembershipsCommandHandler> _logger;

    public RepairClassChatMembershipsCommandHandler(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        IClassChatMembershipSynchronizer synchronizer,
        ILogger<RepairClassChatMembershipsCommandHandler> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _synchronizer = synchronizer;
        _logger = logger;
    }

    public async Task<Result<ChatMembershipSyncResponse>> HandleAsync(
        Guid classId,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (!ClassAuthorizationRules.IsStaff(currentUserRole))
        {
            return Failure(
                ErrorCodes.ClassAccessDenied,
                "Only an administrator or assigned lecturer can repair class chat memberships.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async transactionToken =>
            {
                var targetClass = await _context.Classes
                    .AsNoTracking()
                    .Where(item => item.Id == classId)
                    .Select(item => new { item.PrimaryLecturerId })
                    .FirstOrDefaultAsync(transactionToken);

                if (targetClass == null)
                {
                    return Failure(ErrorCodes.ClassNotFound, "The requested class was not found.");
                }

                if (!ClassAuthorizationRules.CanManageClass(
                        targetClass.PrimaryLecturerId,
                        currentUserId,
                        currentUserRole))
                {
                    return Failure(
                        ErrorCodes.ClassAccessDenied,
                        "You can only repair chat memberships for classes assigned to you.");
                }

                var summary = await _synchronizer.SynchronizeAsync(
                    classId,
                    currentUserId,
                    transactionToken);

                _context.ClassAuditLogs.Add(new ClassAuditLog
                {
                    ClassId = classId,
                    Action = "CHAT_MEMBERSHIPS_REPAIRED",
                    PerformedByUserId = currentUserId,
                    OccurredAtUtc = DateTime.UtcNow,
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        summary.GroupsCreated,
                        summary.MembershipsAdded,
                        summary.MembershipsReactivated,
                        summary.MembershipsEnded,
                        summary.IsReadOnly
                    })
                });

                await _unitOfWork.SaveChangesAsync(transactionToken);
                return Result.Success(summary);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Class chat membership repair failed for class {ClassId}, requested by {UserId}",
                classId,
                currentUserId);

            return Failure(
                ErrorCodes.ClassChatMembershipRepairFailed,
                "Could not repair class chat memberships. Please try again or contact an administrator.");
        }
    }

    private static Result<ChatMembershipSyncResponse> Failure(string code, string message) =>
        Result.Failure<ChatMembershipSyncResponse>(new Error(code, message));
}
