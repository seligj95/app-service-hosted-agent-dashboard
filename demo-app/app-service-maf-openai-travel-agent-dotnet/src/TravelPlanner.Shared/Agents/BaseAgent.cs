using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.Services;
using Azure.AppService.AgentDashboard.Extensions;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Base implementation for Agent Framework ChatClientAgent-based agents
/// </summary>
public abstract class BaseAgent : IAgent
{
    protected readonly ILogger Logger;
    protected readonly AgentOptions Options;
    protected readonly ChatClientAgent Agent;
    
    public abstract string AgentType { get; }
    protected abstract string AgentName { get; }
    protected abstract string Instructions { get; }
    
    // Constructor for simple agents without tools
    protected BaseAgent(
        ILogger logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        Logger = logger;
        Options = options.Value;
        
        var agentChatOptions = new ChatOptions { Instructions = Instructions }
            .WithAgentName(AgentType);

        Agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = agentChatOptions
        }, loggerFactory, serviceProvider);
    }
    
    // Constructor for agents with tools
    protected BaseAgent(
        ILogger logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ChatOptions chatOptions,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider)
    {
        Logger = logger;
        Options = options.Value;
        
        chatOptions.Instructions = Instructions;
        chatOptions.WithAgentName(AgentType);

        Agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = chatOptions
        }, loggerFactory, serviceProvider);
    }
    
    public async Task<ChatMessage> InvokeAsync(IList<ChatMessage> chatHistory, CancellationToken cancellationToken = default)
    {
        var session = await Agent.CreateSessionAsync(cancellationToken);
        var response = await Agent.RunAsync(chatHistory, session, options: null, cancellationToken);
        return response.Messages.LastOrDefault() ?? new ChatMessage(ChatRole.Assistant, "No response generated.");
    }
}
