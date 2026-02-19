using System.Collections.Concurrent;

namespace Worker.Observability;

public sealed class InMemoryMetrics
{
    private readonly ConcurrentQueue<CycleSnapshot> _lastCycles = new();
    private readonly int _max;

    private long _cycles;
    private long _quotesInserted;
    private long _conflicts;
    private long _errors;
    private long _elapsedTotalMs;

    public InMemoryMetrics(int maxSnapshots = 20) => _max = maxSnapshots;

    public void Record(string cycleId, long elapsedMs, CycleCounters c)
    {
        Interlocked.Increment(ref _cycles);
        Interlocked.Add(ref _quotesInserted, c.QuotesInserted);
        Interlocked.Add(ref _conflicts, c.QuotesConflictSkipped);
        Interlocked.Add(ref _errors, c.Errors);
        Interlocked.Add(ref _elapsedTotalMs, elapsedMs);

        _lastCycles.Enqueue(new CycleSnapshot(cycleId, elapsedMs, c));
        while (_lastCycles.Count > _max && _lastCycles.TryDequeue(out _)) { }
    }

    public MetricsSnapshot Snapshot()
    {
        var cycles = Interlocked.Read(ref _cycles);
        var elapsed = Interlocked.Read(ref _elapsedTotalMs);

        return new MetricsSnapshot(
            cycles,
            Interlocked.Read(ref _quotesInserted),
            Interlocked.Read(ref _conflicts),
            Interlocked.Read(ref _errors),
            cycles == 0 ? 0 : (double)elapsed / cycles,
            _lastCycles.ToArray()
        );
    }
}

public readonly record struct CycleSnapshot(string CycleId, long ElapsedMs, CycleCounters Counters);

public readonly record struct MetricsSnapshot(
    long Cycles,
    long QuotesInserted,
    long Conflicts,
    long Errors,
    double AvgElapsedMs,
    CycleSnapshot[] LastCycles
);