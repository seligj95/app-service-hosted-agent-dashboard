using Azure.AppService.AgentDashboard.Models;
using Azure.AppService.AgentDashboard.Telemetry;
using Xunit;

namespace Azure.AppService.AgentDashboard.Tests;

public class AgentTelemetryStoreTests
{
    [Fact]
    public void Record_EnforcesRingBufferMax()
    {
        var store = new AgentTelemetryStore(maxEvents: 100);

        for (int i = 0; i < 150; i++)
        {
            store.Record(MakeEvent($"Agent{i % 3}", TimeSpan.FromMilliseconds(i)));
        }

        Assert.Equal(100, store.Count);
        var events = store.GetRecentEvents(200);
        Assert.Equal(100, events.Count);
    }

    [Fact]
    public void GetRecentEvents_ReturnsNewestFirst()
    {
        var store = new AgentTelemetryStore();
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(1), id: "first"));
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(2), id: "second"));
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(3), id: "third"));

        var events = store.GetRecentEvents(2);
        Assert.Equal(2, events.Count);
        Assert.Equal("third", events[0].Id);
        Assert.Equal("second", events[1].Id);
    }

    [Fact]
    public void GetRecentEvents_FiltersbyAgentName()
    {
        var store = new AgentTelemetryStore();
        store.Record(MakeEvent("Alpha"));
        store.Record(MakeEvent("Beta"));
        store.Record(MakeEvent("Alpha"));
        store.Record(MakeEvent("Gamma"));

        var alphaEvents = store.GetRecentEvents(50, "Alpha");
        Assert.Equal(2, alphaEvents.Count);
        Assert.All(alphaEvents, e => Assert.Equal("Alpha", e.AgentName));
    }

    [Fact]
    public void GetMetrics_ComputesCorrectAggregations()
    {
        var store = new AgentTelemetryStore();

        // Agent A: 3 successes with known durations and tokens
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(100), inputTokens: 10, outputTokens: 20, totalTokens: 30));
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(200), inputTokens: 15, outputTokens: 25, totalTokens: 40));
        store.Record(MakeEvent("A", TimeSpan.FromMilliseconds(300), inputTokens: 5, outputTokens: 10, totalTokens: 15));

        // Agent B: 1 success, 1 error
        store.Record(MakeEvent("B", TimeSpan.FromMilliseconds(50)));
        store.Record(MakeEvent("B", TimeSpan.FromMilliseconds(150), success: false, errorMessage: "fail"));

        var metrics = store.GetMetrics();
        Assert.Equal(2, metrics.Count);

        var a = metrics.First(m => m.AgentName == "A");
        Assert.Equal(3, a.InvocationCount);
        Assert.Equal(200, a.AvgLatencyMs); // (100+200+300)/3
        Assert.Equal(0, a.ErrorCount);
        Assert.Equal(0, a.ErrorRate);
        Assert.Equal(30, a.TotalInputTokens);
        Assert.Equal(55, a.TotalOutputTokens);
        Assert.Equal(85, a.TotalTokens);

        var b = metrics.First(m => m.AgentName == "B");
        Assert.Equal(2, b.InvocationCount);
        Assert.Equal(1, b.ErrorCount);
        Assert.Equal(0.5, b.ErrorRate);
    }

    [Fact]
    public void GetAgentNames_ReturnsDistinctNames()
    {
        var store = new AgentTelemetryStore();
        store.Record(MakeEvent("X"));
        store.Record(MakeEvent("Y"));
        store.Record(MakeEvent("X"));
        store.Record(MakeEvent("Z"));

        var names = store.GetAgentNames();
        Assert.Equal(3, names.Count);
        Assert.Contains("X", names);
        Assert.Contains("Y", names);
        Assert.Contains("Z", names);
    }

    [Fact]
    public void ThreadSafety_ParallelWrites()
    {
        var store = new AgentTelemetryStore(maxEvents: 500);

        Parallel.For(0, 1000, i =>
        {
            store.Record(MakeEvent($"Agent{i % 5}", TimeSpan.FromMilliseconds(i)));
        });

        // Count should be <= 500 (ring buffer) and > 0
        Assert.True(store.Count > 0);
        Assert.True(store.Count <= 500);

        // Metrics should not throw
        var metrics = store.GetMetrics();
        Assert.NotNull(metrics);
    }

    [Fact]
    public void GetMetrics_PercentilesCorrect()
    {
        var store = new AgentTelemetryStore();

        // Add 100 events with durations 1..100ms
        for (int i = 1; i <= 100; i++)
        {
            store.Record(MakeEvent("P", TimeSpan.FromMilliseconds(i)));
        }

        var metrics = store.GetMetrics();
        var p = metrics.Single();
        Assert.Equal(50.5, p.AvgLatencyMs);
        Assert.Equal(50.5, p.P50LatencyMs);
        Assert.Equal(95.05, p.P95LatencyMs);
        Assert.Equal(99.01, p.P99LatencyMs);
    }

    private static AgentInvocationEvent MakeEvent(
        string agentName,
        TimeSpan? duration = null,
        bool success = true,
        string? errorMessage = null,
        int? inputTokens = null,
        int? outputTokens = null,
        int? totalTokens = null,
        string? id = null)
    {
        return new AgentInvocationEvent
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            AgentName = agentName,
            Timestamp = DateTime.UtcNow,
            Duration = duration ?? TimeSpan.FromMilliseconds(100),
            Success = success,
            ErrorMessage = errorMessage,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = totalTokens,
            MessageCount = 1
        };
    }
}
