using Azure.AppService.AgentDashboard.Models;

namespace Azure.AppService.AgentDashboard.Options;

public sealed class AgentDashboardOptions
{
    public string RoutePrefix { get; set; } = "/agents";
    public int MaxStoredEvents { get; set; } = 1000;
    public List<AgentRegistration> RegisteredAgents { get; set; } = [];
    public AgentTopology? Topology { get; set; }

    /// <summary>
    /// When set, telemetry events are persisted to this file path (JSONL format).
    /// Both API and WebJob processes can share the same file for cross-process telemetry.
    /// Events from the file are merged into the in-memory store on read operations.
    /// </summary>
    public string? SharedFilePath { get; set; }
}
