using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.Models;
using TravelPlanner.Shared.Services;
using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Optimizes budget allocation and provides cost estimates
/// </summary>
public class BudgetOptimizerAgent : BaseAgent
{
    public override string AgentType => "BudgetOptimizer";
    protected override string AgentName => "Budget Optimization Specialist";
    
    protected override string Instructions => "You are a travel budget optimization expert. Allocate budgets across accommodation, food, activities, and transport. Provide realistic cost estimates, suggest cost-saving strategies, identify low-cost alternatives, and always include an emergency fund.";

    public BudgetOptimizerAgent(
        ILogger<BudgetOptimizerAgent> logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) 
        : base(logger, options, chatClient, loggerFactory, serviceProvider)
    {
    }
}
