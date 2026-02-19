using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Base interface for all Agent Framework agents
/// </summary>
public interface IAgent
{
    /// <summary>
    /// The unique identifier for this agent type
    /// </summary>
    string AgentType { get; }
    
    /// <summary>
    /// Invoke the agent with a chat history
    /// </summary>
    Task<ChatMessage> InvokeAsync(IList<ChatMessage> chatHistory, CancellationToken cancellationToken = default);
}
