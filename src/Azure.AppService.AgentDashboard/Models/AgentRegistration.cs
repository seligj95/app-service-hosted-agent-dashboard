namespace Azure.AppService.AgentDashboard.Models;

public sealed record AgentRegistration
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? AgentType { get; init; }
    public bool AutoDiscovered { get; init; }
}
