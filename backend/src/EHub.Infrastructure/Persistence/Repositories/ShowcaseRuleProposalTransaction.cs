using System.Data;
using System.Data.Common;
using EHub.Application.Common.Exceptions;
using EHub.Application.Features.Showcase.Reports;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EHub.Infrastructure.Persistence.Repositories;

public sealed class ShowcaseRuleProposalTransaction(AppDbContext context, ILogger<ShowcaseRuleProposalTransaction> logger)
    : IShowcaseRuleProposalTransaction
{
    public async Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation, CancellationToken token = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            var result = await operation(token);
            await transaction.CommitAsync(token);
            context.SignalCommittedOutbox();
            return result;
        }
        catch (Exception failure)
        {
            // PostgreSQL can already have rolled back a serialization failure raised at COMMIT.
            // A second rollback must never replace that original failure with a completed-transaction exception.
            try
            {
                if (transaction.GetDbTransaction().Connection is not null)
                    await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackFailure) when (rollbackFailure is InvalidOperationException or DbException)
            {
                logger.LogDebug(rollbackFailure, "Showcase rule transaction rollback was already completed or unavailable.");
            }
            context.DiscardUncommittedOutboxSignal();
            for (Exception? current = failure; current is not null; current = current.InnerException)
            {
                if (current is DbUpdateConcurrencyException || current is PostgresException
                    { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected })
                    throw new SerializableTransactionConflictException("Concurrent Showcase rule changes conflicted.", failure);
            }
            throw;
        }
    }
}
