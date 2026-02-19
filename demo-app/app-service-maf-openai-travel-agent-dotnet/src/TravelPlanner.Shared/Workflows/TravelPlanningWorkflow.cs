using Microsoft.Extensions.Logging;
using TravelPlanner.Shared.Agents;
using TravelPlanner.Shared.Models;
using TravelPlanner.Shared.ExternalServices;
using Microsoft.Extensions.AI;

namespace TravelPlanner.Shared.Workflows;

/// <summary>
/// Orchestrates the multi-agent travel planning workflow
/// </summary>
public class TravelPlanningWorkflow
{
    private readonly ILogger<TravelPlanningWorkflow> _logger;
    private readonly CoordinatorAgent _coordinatorAgent;
    private readonly CurrencyConverterAgent _currencyAgent;
    private readonly WeatherAdvisorAgent _weatherAgent;
    private readonly LocalKnowledgeAgent _localKnowledgeAgent;
    private readonly ItineraryPlannerAgent _itineraryAgent;
    private readonly BudgetOptimizerAgent _budgetAgent;
    
    public TravelPlanningWorkflow(
        ILogger<TravelPlanningWorkflow> logger,
        CoordinatorAgent coordinatorAgent,
        CurrencyConverterAgent currencyAgent,
        WeatherAdvisorAgent weatherAgent,
        LocalKnowledgeAgent localKnowledgeAgent,
        ItineraryPlannerAgent itineraryAgent,
        BudgetOptimizerAgent budgetAgent)
    {
        _logger = logger;
        _coordinatorAgent = coordinatorAgent;
        _currencyAgent = currencyAgent;
        _weatherAgent = weatherAgent;
        _localKnowledgeAgent = localKnowledgeAgent;
        _itineraryAgent = itineraryAgent;
        _budgetAgent = budgetAgent;
    }
    
    public async Task<TravelItinerary> ExecuteAsync(
        TravelPlanRequest request,
        string taskId,
        IProgress<WorkflowProgress> progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting multi-agent workflow for task {TaskId}", taskId);
        
        var state = new WorkflowState { TaskId = taskId };
        var days = (request.EndDate - request.StartDate).Days + 1;
        
        try
        {
            // PHASE 1: Parallel Information Gathering (10% - 40%)
            progress.Report(new WorkflowProgress(10, "Gathering destination information...", "Workflow"));
            
            var gatheringTasks = new[]
            {
                GatherCurrencyInfoAsync(request, state, progress, cancellationToken),
                GatherWeatherInfoAsync(request, state, progress, cancellationToken),
                GatherLocalKnowledgeAsync(request, state, progress, cancellationToken)
            };
            
            await Task.WhenAll(gatheringTasks);
            state.CurrentPhase = 1;
            state.MarkStepComplete("InformationGathering");
            
            // PHASE 2: Itinerary Planning (40% - 70%)
            progress.Report(new WorkflowProgress(40, "Creating personalized itinerary...", "ItineraryPlanner"));
            
            var localKnowledge = state.GetFromContext<string>("LocalKnowledge") ?? "";
            var weatherAdvice = state.GetFromContext<string>("WeatherAdvice") ?? "";
            
            var itineraryChatHistory = state.GetChatHistory("ItineraryPlanner");
            itineraryChatHistory.Add(new ChatMessage(ChatRole.User,
                $"Create a detailed {days}-day itinerary for {request.Destination} from {request.StartDate:MMM dd} to {request.EndDate:MMM dd}. " +
                $"Budget: ${request.Budget:N0} USD. " +
                $"Interests: {string.Join(", ", request.Interests)}. " +
                $"Travel Style: {request.TravelStyle}. " +
                $"{(string.IsNullOrEmpty(request.SpecialRequests) ? "" : $"Special Requests: {request.SpecialRequests}. ")}" +
                $"\n\nWEATHER INFORMATION:\n{weatherAdvice}\n\n" +
                $"LOCAL KNOWLEDGE & TIPS:\n{localKnowledge}\n\n" +
                $"Please create a comprehensive day-by-day itinerary with specific activities, timing, costs, and practical tips for each day."));
            
            var itineraryResponse = await _itineraryAgent.InvokeAsync(itineraryChatHistory, cancellationToken);
            var itinerary = itineraryResponse.Text ?? "";
            
            itineraryChatHistory.Add(itineraryResponse);
            state.AddToContext("Itinerary", itinerary);
            state.CurrentPhase = 2;
            state.MarkStepComplete("ItineraryPlanning");
            
            // PHASE 3: Budget Optimization (70% - 85%)
            progress.Report(new WorkflowProgress(70, "Optimizing budget allocation...", "BudgetOptimizer"));
            
            var budgetChatHistory = state.GetChatHistory("BudgetOptimizer");
            budgetChatHistory.Add(new ChatMessage(ChatRole.User,
                $"Optimize the budget allocation for a {days}-day trip to {request.Destination}. " +
                $"Total Budget: ${request.Budget:N0} USD. " +
                $"Travel Style: {request.TravelStyle}. " +
                $"\n\nPLANNED ACTIVITIES:\n{(itinerary.Length > 1000 ? itinerary.Substring(0, 1000) + "..." : itinerary)}\n\n" +
                $"Provide a detailed budget breakdown with specific dollar amounts for: " +
                $"Accommodation, Food & Dining, Activities & Attractions, Transportation, Shopping & Souvenirs, and Emergency Fund. " +
                $"Include daily budget guidelines and cost-saving tips specific to {request.Destination}."));
            
            var budgetResponse = await _budgetAgent.InvokeAsync(budgetChatHistory, cancellationToken);
            budgetChatHistory.Add(budgetResponse);
            
            state.AddToContext("BudgetAdvice", budgetResponse.Text ?? "");
            state.CurrentPhase = 3;
            state.MarkStepComplete("BudgetOptimization");
            
            // PHASE 4: Final Assembly (85% - 100%)
            progress.Report(new WorkflowProgress(85, "Assembling complete travel plan...", "Coordinator"));
            
            var coordinatorChatHistory = state.GetChatHistory("Coordinator");
            coordinatorChatHistory.Add(new ChatMessage(ChatRole.User,
                $"You are assembling a complete travel plan for {request.Destination} from {request.StartDate:MMM dd} to {request.EndDate:MMM dd} ({days} days). " +
                $"Budget: ${request.Budget:N0} USD. Travel Style: {request.TravelStyle}. " +
                $"Interests: {string.Join(", ", request.Interests)}. " +
                $"{(string.IsNullOrEmpty(request.SpecialRequests) ? "" : $"Special Requests: {request.SpecialRequests}. ")}" +
                $"\n\nITINERARY:\n{itinerary}\n\n" +
                $"BUDGET ADVICE:\n{state.GetFromContext<string>("BudgetAdvice") ?? "N/A"}\n\n" +
                $"CURRENCY INFO:\n{state.GetFromContext<string>("CurrencyAdvice") ?? "N/A"}\n\n" +
                $"WEATHER INFO:\n{state.GetFromContext<string>("WeatherAdvice") ?? "N/A"}\n\n" +
                $"LOCAL KNOWLEDGE:\n{state.GetFromContext<string>("LocalKnowledge") ?? "N/A"}\n\n" +
                $"Please synthesize all the above information into a cohesive final travel plan summary. " +
                $"Include: key highlights, a packing list based on weather and activities, " +
                $"budget summary, essential travel tips, and any important warnings or recommendations."));

            var coordinatorResponse = await _coordinatorAgent.InvokeAsync(coordinatorChatHistory, cancellationToken);
            coordinatorChatHistory.Add(coordinatorResponse);
            var finalSummary = coordinatorResponse.Text ?? "";

            var result = AssembleFinalItinerary(request, taskId, state, itinerary, finalSummary);
            
            state.CurrentPhase = 4;
            state.MarkStepComplete("FinalAssembly");
            
            progress.Report(new WorkflowProgress(100, "Travel plan complete!", "Workflow"));
            
            _logger.LogInformation("Multi-agent workflow completed for task {TaskId}", taskId);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-agent workflow for task {TaskId}", taskId);
            throw;
        }
    }
    
    private async Task GatherCurrencyInfoAsync(
        TravelPlanRequest request,
        WorkflowState state,
        IProgress<WorkflowProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress.Report(new WorkflowProgress(15, "Converting budget to local currency...", "CurrencyConverter"));
            
            // Determine destination currency (simplified - in production, use a currency mapping service)
            var destinationCurrency = GetDestinationCurrency(request.Destination);
            
            if (destinationCurrency != "USD")
            {
                var chatHistory = state.GetChatHistory("CurrencyConverter");
                chatHistory.Add(new ChatMessage(ChatRole.User, 
                    $"Provide currency advice for a traveler going to {request.Destination}. " +
                    $"Their budget is ${request.Budget} USD and they want to convert it to {destinationCurrency}. " +
                    $"Please convert the amount using real-time exchange rates and provide advice on: " +
                    $"1) Current exchange rate and what it means for their budget, " +
                    $"2) Best practices for currency exchange, " +
                    $"3) Typical costs in {request.Destination}, " +
                    $"4) Currency-related tips or warnings."));
                
                var response = await _currencyAgent.InvokeAsync(chatHistory, cancellationToken);
                var responseText = response.Text ?? "";
                
                chatHistory.Add(response);
                state.AddToContext("CurrencyAdvice", responseText);
                
                _logger.LogInformation("Currency advice gathered for {Currency}", destinationCurrency);
            }
            
            state.MarkStepComplete("CurrencyGathering");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error gathering currency info, continuing without it");
        }
    }
    
    private async Task GatherWeatherInfoAsync(
        TravelPlanRequest request,
        WorkflowState state,
        IProgress<WorkflowProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress.Report(new WorkflowProgress(20, "Fetching weather forecast...", "WeatherAdvisor"));
            
            var (lat, lon) = GetDestinationCoordinates(request.Destination);
            var days = (request.EndDate - request.StartDate).Days + 1;
            var hasCoords = lat != 0 && lon != 0;
            
            var chatHistory = state.GetChatHistory("WeatherAdvisor");
            
            if (hasCoords)
            {
                chatHistory.Add(new ChatMessage(ChatRole.User,
                    $"Provide weather-based travel advice for {request.Destination} from {request.StartDate:MMM dd} to {request.EndDate:MMM dd} ({days} days). " +
                    $"Get the weather forecast for latitude {lat}, longitude {lon} for {days} days. " +
                    $"The traveler's interests are: {string.Join(", ", request.Interests)}. " +
                    $"Please provide: " +
                    $"1) Weather overview and what to expect, " +
                    $"2) Detailed packing list based on conditions, " +
                    $"3) Activity recommendations that work well with this weather, " +
                    $"4) Any weather-related warnings or precautions, " +
                    $"5) Best times of day for outdoor activities."));
            }
            else
            {
                chatHistory.Add(new ChatMessage(ChatRole.User,
                    $"Provide weather-based travel advice for {request.Destination} from {request.StartDate:MMM dd} to {request.EndDate:MMM dd} ({days} days). " +
                    $"I don't have exact coordinates, so use your knowledge of typical weather patterns for {request.Destination} during this time of year. " +
                    $"The traveler's interests are: {string.Join(", ", request.Interests)}. " +
                    $"Please provide: " +
                    $"1) Typical weather overview and what to expect, " +
                    $"2) Detailed packing list based on expected conditions, " +
                    $"3) Activity recommendations that work well with this weather, " +
                    $"4) Any weather-related warnings or precautions, " +
                    $"5) Best times of day for outdoor activities."));
            }
            
            var response = await _weatherAgent.InvokeAsync(chatHistory, cancellationToken);
            var responseText = response.Text ?? "";
            
            chatHistory.Add(response);
            state.AddToContext("WeatherAdvice", responseText);
            
            _logger.LogInformation("Weather advice gathered for {Destination}", request.Destination);
            
            state.MarkStepComplete("WeatherGathering");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error gathering weather info, continuing without it");
        }
    }
    
    private async Task GatherLocalKnowledgeAsync(
        TravelPlanRequest request,
        WorkflowState state,
        IProgress<WorkflowProgress> progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress.Report(new WorkflowProgress(25, "Gathering local knowledge and tips...", "LocalKnowledge"));
            
            var chatHistory = state.GetChatHistory("LocalKnowledge");
            chatHistory.Add(new ChatMessage(ChatRole.User,
                $"Provide comprehensive local knowledge for {request.Destination}. " +
                $"Traveler interests: {string.Join(", ", request.Interests)}. " +
                $"{(string.IsNullOrEmpty(request.SpecialRequests) ? "" : $"Special requests: {request.SpecialRequests}. ")}" +
                $"Please provide: " +
                $"1) Cultural insights (customs, etiquette, dress codes, tipping), " +
                $"2) Safety & practical info (safety tips, emergency numbers, embassy info), " +
                $"3) Transportation (how to get around, apps, costs), " +
                $"4) Local favorites (hidden gems, authentic experiences, food specialties), " +
                $"5) Communication (common phrases, English availability, translation apps), " +
                $"6) Practical tips (best areas to stay, shop hours, common scams, cell phone options)."));
            
            var response = await _localKnowledgeAgent.InvokeAsync(chatHistory, cancellationToken);
            var responseText = response.Text ?? "";
            
            chatHistory.Add(response);
            state.AddToContext("LocalKnowledge", responseText);
            
            _logger.LogInformation("Retrieved local knowledge for {Destination}", request.Destination);
            
            state.MarkStepComplete("LocalKnowledgeGathering");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error gathering local knowledge, continuing without it");
        }
    }
    
    private TravelItinerary AssembleFinalItinerary(
        TravelPlanRequest request,
        string taskId,
        WorkflowState state,
        string itineraryText,
        string coordinatorSummary)
    {
        var days = (request.EndDate - request.StartDate).Days + 1;
        var currencyAdvice = state.GetFromContext<string>("CurrencyAdvice");
        var weatherAdvice = state.GetFromContext<string>("WeatherAdvice");
        var localKnowledge = state.GetFromContext<string>("LocalKnowledge");
        var budgetAdvice = state.GetFromContext<string>("BudgetAdvice");
        
        // Build travel tips from various sources
        var travelTips = new List<string>
        {
            "💱 Review the currency conversion advice above",
            "☀️ Check the weather packing recommendations",
            "📱 Download offline maps of your destination",
            "💳 Notify your bank of travel dates to avoid card issues",
            "📋 Keep copies of important documents (passport, insurance)"
        };
        
        // Create basic packing list
        var packingList = new List<string>
        {
            "Passport and travel documents",
            "Phone charger and power adapter",
            "Comfortable walking shoes",
            "Reusable water bottle",
            "Basic first aid kit",
            "Weather-appropriate clothing (see weather advice)",
            "Travel insurance documentation"
        };
        
        // Add weather-specific items if weather advice contains specific info
        if (!string.IsNullOrEmpty(weatherAdvice))
        {
            if (weatherAdvice.Contains("cold", StringComparison.OrdinalIgnoreCase) || 
                weatherAdvice.Contains("winter", StringComparison.OrdinalIgnoreCase))
            {
                packingList.Add("Warm jacket and layers");
                packingList.Add("Cold weather accessories (hat, gloves)");
            }
            
            if (weatherAdvice.Contains("hot", StringComparison.OrdinalIgnoreCase) || 
                weatherAdvice.Contains("summer", StringComparison.OrdinalIgnoreCase) ||
                weatherAdvice.Contains("warm", StringComparison.OrdinalIgnoreCase))
            {
                packingList.Add("Sunscreen and sunglasses");
                packingList.Add("Light, breathable clothing");
            }
            
            if (weatherAdvice.Contains("rain", StringComparison.OrdinalIgnoreCase))
            {
                packingList.Add("Rain jacket or umbrella");
            }
        }
        
        // Add interest-specific items
        if (request.Interests.Any(i => i.Contains("hiking", StringComparison.OrdinalIgnoreCase)))
        {
            packingList.Add("Hiking boots and daypack");
        }
        
        // Create a simplified budget breakdown
        var budgetBreakdown = new BudgetBreakdown
        {
            TotalBudget = request.Budget,
            Accommodation = request.Budget * 0.35m,
            Food = request.Budget * 0.25m,
            Activities = request.Budget * 0.20m,
            Transportation = request.Budget * 0.10m,
            Shopping = request.Budget * 0.05m,
            Emergency = request.Budget * 0.05m
        };
        
        return new TravelItinerary
        {
            TaskId = taskId,
            Destination = request.Destination,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DailyPlans = new List<DayPlan>
            {
                // Store the full itinerary text in a single day plan for now
                // In production, you'd parse the itinerary text into structured days
                new DayPlan
                {
                    DayNumber = 1,
                    Date = request.StartDate,
                    Theme = $"{days}-Day {request.Destination} Itinerary",
                    Morning = new Activity
                    {
                        Location = request.Destination,
                        Description = $"{coordinatorSummary}\n\n---\n\nDETAILED ITINERARY:\n{itineraryText}",
                        EstimatedCost = 0
                    }
                }
            },
            Budget = budgetBreakdown,
            TravelTips = travelTips,
            PackingList = packingList,
            EmergencyContacts = new EmergencyInfo
            {
                LocalEmergencyNumber = "112 (EU) or 911 (US/Canada)",
                NearestEmbassy = $"Contact your embassy in {request.Destination}",
                HealthcareInfo = "Travel with comprehensive health insurance."
            }
        };
    }
    
    // Helper methods for destination data (simplified - in production, use proper services)
    private string GetDestinationCurrency(string destination)
    {
        // Simplified mapping - in production, use a proper currency/country database
        return destination.ToLower() switch
        {
            var d when d.Contains("paris") || d.Contains("france") => "EUR",
            var d when d.Contains("london") || d.Contains("uk") || d.Contains("england") => "GBP",
            var d when d.Contains("tokyo") || d.Contains("japan") => "JPY",
            var d when d.Contains("mexico") => "MXN",
            var d when d.Contains("canada") => "CAD",
            _ => "USD" // Default
        };
    }
    
    private (double lat, double lon) GetDestinationCoordinates(string destination)
    {
        // Simplified mapping - in production, use a geocoding service
        // NWS API only works for US locations
        return destination.ToLower() switch
        {
            var d when d.Contains("new york") => (40.7128, -74.0060),
            var d when d.Contains("los angeles") => (34.0522, -118.2437),
            var d when d.Contains("chicago") => (41.8781, -87.6298),
            var d when d.Contains("san francisco") => (37.7749, -122.4194),
            var d when d.Contains("miami") => (25.7617, -80.1918),
            var d when d.Contains("seattle") => (47.6062, -122.3321),
            var d when d.Contains("boston") => (42.3601, -71.0589),
            var d when d.Contains("washington") || d.Contains("dc") => (38.9072, -77.0369),
            _ => (0, 0) // Unknown - will be handled gracefully
        };
    }
}
