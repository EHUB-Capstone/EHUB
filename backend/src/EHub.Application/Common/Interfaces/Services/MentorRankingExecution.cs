namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Scoped execution budget and progress for one mentor allocation.</summary>
public sealed class MentorRankingExecution
{
    public const int ProjectsPerBatch = 20;
    private Func<int, int, CancellationToken, Task>? report;
    private int completed;
    private int total;
    public TimeSpan Timeout { get; private set; } = TimeSpan.FromSeconds(180);

    public void EnableBackground(Func<int, int, CancellationToken, Task> progress)
    {
        Timeout = TimeSpan.FromMinutes(15);
        report = progress;
    }

    public Task BeginAsync(int totalBatches, CancellationToken ct)
    {
        total = totalBatches;
        completed = 0;
        return report?.Invoke(completed, total, ct) ?? Task.CompletedTask;
    }

    public Task AdvanceAsync(CancellationToken ct) =>
        report?.Invoke(++completed, total, ct) ?? Task.CompletedTask;
}
