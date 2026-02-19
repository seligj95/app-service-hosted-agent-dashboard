using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.Services;
using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Provides destination-specific knowledge, culture, safety, and local tips
/// </summary>
public class LocalKnowledgeAgent : BaseAgent
{
    public override string AgentType => "LocalKnowledge";
    protected override string AgentName => "Local Expert & Cultural Guide";
    
    protected override string Instructions => "You are a local knowledge expert. Provide cultural insights, safety tips, local transportation, authentic experiences, customs, tipping practices, emergency contacts, useful phrases, and common scams. Help travelers feel confident and respectful in their destination.";

    public LocalKnowledgeAgent(
        ILogger<LocalKnowledgeAgent> logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) 
        : base(logger, options, chatClient, loggerFactory, serviceProvider)
    {
    }
}
