using System.Collections.Concurrent;
using System.Text.Json;
using Azure.AppService.AgentDashboard.Models;

namespace Azure.AppService.AgentDashboard.Telemetry;

public sealed class AgentTelemetryStore
{
    private readonly ConcurrentQueue<AgentInvocationEvent> _events = new();
    private readonly int _maxEvents;
    private int _count;
    private readonly string? _sharedFilePath;
    private readonly object _fileLock = new();
    private readonly HashSet<string> _knownIds = new();
    private long _lastFilePosition;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DateTime UptimeSince { get; } = DateTime.UtcNow;

    public AgentTelemetryStore(int maxEvents = 1000, string? sharedFilePath = null)
    {
        _maxEvents = maxEvents;
        _sharedFilePath = sharedFilePath;

        if (_sharedFilePath != null)
        {
            var dir = Path.GetDirectoryName(_sharedFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            SyncFromFile();
        }
    }

    public void Record(AgentInvocationEvent invocationEvent)
    {
        EnqueueEvent(invocationEvent);

        if (_sharedFilePath != null)
            AppendToFile(invocationEvent);
    }

    public IReadOnlyList<AgentInvocationEvent> GetRecentEvents(int limit = 50, string? agentName = null)
    {
        SyncFromFile();

        var query = _events.AsEnumerable();

        if (!string.IsNullOrEmpty(agentName))
            query = query.Where(e => e.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

        return query.Reverse().Take(limit).ToList();
    }

    public IReadOnlyList<AgentMetrics> GetMetrics()
    {
        SyncFromFile();

        var snapshot = _events.ToArray();
        return snapshot
            .GroupBy(e => e.AgentName)
            .Select(g =>
            {
                var events = g.ToList();
                var durations = events.Select(e => e.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
                var errorCount = events.Count(e => !e.Success);

                return new AgentMetrics
                {
                    AgentName = g.Key,
                    InvocationCount = events.Count,
                    AvgLatencyMs = durations.Count > 0 ? Math.Round(durations.Average(), 2) : 0,
                    P50LatencyMs = Percentile(durations, 0.50),
                    P95LatencyMs = Percentile(durations, 0.95),
                    P99LatencyMs = Percentile(durations, 0.99),
                    ErrorCount = errorCount,
                    ErrorRate = events.Count > 0 ? Math.Round((double)errorCount / events.Count, 4) : 0,
                    TotalInputTokens = events.Sum(e => (long)(e.InputTokens ?? 0)),
                    TotalOutputTokens = events.Sum(e => (long)(e.OutputTokens ?? 0)),
                    TotalTokens = events.Sum(e => (long)(e.TotalTokens ?? 0))
                };
            })
            .ToList();
    }

    public IReadOnlyList<string> GetAgentNames()
    {
        SyncFromFile();
        return _events.Select(e => e.AgentName).Distinct().ToList();
    }

    public int Count => Volatile.Read(ref _count);

    private void EnqueueEvent(AgentInvocationEvent evt)
    {
        lock (_knownIds)
        {
            if (!_knownIds.Add(evt.Id))
                return; // duplicate
        }

        _events.Enqueue(evt);
        var newCount = Interlocked.Increment(ref _count);

        while (newCount > _maxEvents && _events.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
            newCount = Volatile.Read(ref _count);
        }
    }

    private void AppendToFile(AgentInvocationEvent evt)
    {
        lock (_fileLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(evt, s_jsonOptions);
                using var stream = new FileStream(_sharedFilePath!, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream);
                writer.WriteLine(json);
                _lastFilePosition = stream.Position;
            }
            catch
            {
                // Silently ignore file write failures to avoid breaking the app
            }
        }
    }

    private void SyncFromFile()
    {
        if (_sharedFilePath == null || !File.Exists(_sharedFilePath))
            return;

        lock (_fileLock)
        {
            try
            {
                using var stream = new FileStream(_sharedFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length <= _lastFilePosition)
                    return; // no new data

                stream.Position = _lastFilePosition;
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var evt = JsonSerializer.Deserialize<AgentInvocationEvent>(line, s_jsonOptions);
                        if (evt != null)
                            EnqueueEvent(evt);
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }

                _lastFilePosition = stream.Position;
            }
            catch
            {
                // Silently ignore file read failures
            }
        }
    }

    private static double Percentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        if (sortedValues.Count == 1) return Math.Round(sortedValues[0], 2);

        var index = percentile * (sortedValues.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        var weight = index - lower;

        var value = sortedValues[lower] * (1 - weight) + sortedValues[upper] * weight;
        return Math.Round(value, 2);
    }
}
