namespace EHub.Application.Common.Interfaces.Services;

/// <summary>
/// Lets a reviewer stop a summary that the worker is generating, so the model call is aborted instead of running to the
/// end. In-process only: with several API instances a cancelled run still finishes, but its result is discarded
/// because the database row is no longer Processing.
/// </summary>
public interface ICheckpointSummaryCancellation
{
    /// <summary>Registers a running summary; dispose the result when the run ends.</summary>
    IDisposable Register(Guid summaryId, out CancellationToken cancellationToken);

    void Cancel(Guid summaryId);
}
