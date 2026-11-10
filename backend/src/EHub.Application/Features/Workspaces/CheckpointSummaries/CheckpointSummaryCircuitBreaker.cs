namespace EHub.Application.Features.Workspaces.CheckpointSummaries;

public enum CheckpointCircuitState
{
    /// <summary>The model answers; files are taken from the queue as usual.</summary>
    Closed,

    /// <summary>The model failed several times in a row; no file is taken until the cool-down ends.</summary>
    Open,

    /// <summary>The cool-down is over; the next file is the test. If it fails the circuit opens again for longer.</summary>
    HalfOpen
}

public sealed record CheckpointCircuitSnapshot(
    CheckpointCircuitState State, int ConsecutiveFailures, DateTime? OpenUntilUtc, DateTime? LastFailureUtc);

/// <summary>
/// Stops the worker from feeding files to a model that is down or stuck. Without it, an Ollama container that is restarting
/// or hung makes every queued file burn one of its attempts (and, for a hung one, wait the full job timeout), so a few minutes
/// of outage can leave dozens of files Failed. Three failures to reach the model in a row open the circuit for 2 minutes; if
/// the first file after that fails too, for 5, then 15. While it is open files keep their attempts and simply wait.
/// In memory and per instance: another API instance finds out for itself.
/// </summary>
public sealed class CheckpointSummaryCircuitBreaker
{
    public const int FailuresToOpen = 3;

    private static readonly TimeSpan[] Cooldowns = [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    private readonly object _gate = new();
    private int _consecutiveFailures;
    private int _trips;
    private DateTime? _openUntil;
    private DateTime? _lastFailure;

    /// <summary>False while the circuit is open. After the cool-down it allows requests again (half open).</summary>
    public bool AllowRequest(DateTime nowUtc)
    {
        lock (_gate) return _openUntil is null || nowUtc >= _openUntil;
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _trips = 0;
            _openUntil = null;
        }
    }

    /// <summary>The model could not be reached or did not answer in time (not a bad file, not a bad answer).</summary>
    public void RecordFailure(DateTime nowUtc)
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            _lastFailure = nowUtc;
            // Also true for the single test request of the half-open state: the count is still above the threshold.
            if (_consecutiveFailures < FailuresToOpen) return;
            _openUntil = nowUtc + Cooldowns[Math.Min(_trips, Cooldowns.Length - 1)];
            _trips++;
        }
    }

    public CheckpointCircuitSnapshot Snapshot(DateTime nowUtc)
    {
        lock (_gate)
        {
            var state = _openUntil is null ? CheckpointCircuitState.Closed
                : nowUtc < _openUntil ? CheckpointCircuitState.Open
                : CheckpointCircuitState.HalfOpen;
            return new CheckpointCircuitSnapshot(state, _consecutiveFailures, _openUntil, _lastFailure);
        }
    }
}
