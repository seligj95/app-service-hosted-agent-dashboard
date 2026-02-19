using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Workflows;

/// <summary>
/// Tracks the state of a multi-agent workflow execution
/// </summary>
public class WorkflowState
{
    public string TaskId { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
    public Dictionary<string, List<ChatMessage>> AgentChatHistories { get; set; } = new();
    public List<string> CompletedSteps { get; set; } = new();
    public int CurrentPhase { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public void AddToContext(string key, object value)
    {
        Context[key] = value;
    }
    
    public T? GetFromContext<T>(string key)
    {
        if (Context.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }
    
    public List<ChatMessage> GetChatHistory(string agentType)
    {
        if (!AgentChatHistories.ContainsKey(agentType))
        {
            AgentChatHistories[agentType] = new List<ChatMessage>();
        }
        return AgentChatHistories[agentType];
    }
    
    public void MarkStepComplete(string stepName)
    {
        if (!CompletedSteps.Contains(stepName))
        {
            CompletedSteps.Add(stepName);
        }
    }
}

/// <summary>
/// Progress update from workflow execution
/// </summary>
public record WorkflowProgress(int Percentage, string Step, string? AgentName = null);
