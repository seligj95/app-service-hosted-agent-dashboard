using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TravelPlanner.Shared.Services;
using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Agents;

/// <summary>
/// Creates detailed day-by-day travel itineraries
/// </summary>
public class ItineraryPlannerAgent : BaseAgent
{
    public override string AgentType => "ItineraryPlanner";
    protected override string AgentName => "Itinerary Planning Expert";
    
    protected override string Instructions => "You are an expert travel itinerary planner. Create detailed day-by-day plans with specific timing, realistic travel times, actual venues, meal recommendations, and weather considerations. Balance popular sites with hidden gems. Match activities to interests and travel style.";

    public ItineraryPlannerAgent(
        ILogger<ItineraryPlannerAgent> logger,
        IOptions<AgentOptions> options,
        IChatClient chatClient,
        ILoggerFactory loggerFactory,
        IServiceProvider serviceProvider) 
        : base(logger, options, chatClient, loggerFactory, serviceProvider)
    {
    }
}
