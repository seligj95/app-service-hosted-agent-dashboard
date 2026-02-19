namespace Azure.AppService.AgentDashboard.Models;

public sealed record AgentInvocationEvent
{
    public required string Id { get; init; }
    public required string AgentName { get; init; }
    public required DateTime Timestamp { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ModelId { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public long? TotalTokens { get; init; }
    public int MessageCount { get; init; }
    public IReadOnlyList<string>? ToolsUsed { get; init; }
}
