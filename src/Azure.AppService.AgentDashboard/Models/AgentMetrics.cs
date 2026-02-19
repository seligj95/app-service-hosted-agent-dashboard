namespace Azure.AppService.AgentDashboard.Models;

public sealed record AgentMetrics
{
    public required string AgentName { get; init; }
    public int InvocationCount { get; init; }
    public double AvgLatencyMs { get; init; }
    public double P50LatencyMs { get; init; }
    public double P95LatencyMs { get; init; }
    public double P99LatencyMs { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public long TotalTokens { get; init; }
}
