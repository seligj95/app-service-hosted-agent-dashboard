namespace Azure.AppService.AgentDashboard.Models;

public sealed record AgentTopology
{
    public IReadOnlyList<TopologyPhase> Phases { get; init; } = [];
}

public sealed record TopologyPhase
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Agents { get; init; } = [];
    public IReadOnlyList<string> NextPhases { get; init; } = [];
}
