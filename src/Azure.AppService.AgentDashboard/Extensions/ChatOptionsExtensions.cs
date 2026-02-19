using Microsoft.Extensions.AI;

namespace Azure.AppService.AgentDashboard.Extensions;

public static class ChatOptionsExtensions
{
    private const string AgentNameKey = "AgentName";

    /// <summary>
    /// Tags a <see cref="ChatOptions"/> with an agent name so the dashboard middleware
    /// can attribute the call to the correct agent in metrics and traces.
    /// </summary>
    public static ChatOptions WithAgentName(this ChatOptions options, string agentName)
    {
        options.AdditionalProperties ??= [];
        options.AdditionalProperties[AgentNameKey] = agentName;
        return options;
    }
}
