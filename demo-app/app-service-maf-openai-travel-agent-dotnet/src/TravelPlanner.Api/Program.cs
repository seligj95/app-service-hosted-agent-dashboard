using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using TravelPlanner.Api.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Azure.AppService.AgentDashboard.Extensions;
using Azure.AppService.AgentDashboard.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure Azure Service Bus
// Priority: Managed Identity (production) > Connection String (local development)
var serviceBusNamespace = builder.Configuration["ServiceBus:Namespace"];
if (!string.IsNullOrEmpty(serviceBusNamespace))
{
    // Production: Use managed identity with Service Bus namespace
    var serviceBusOptions = new ServiceBusClientOptions
    {
        TransportType = ServiceBusTransportType.AmqpWebSockets
    };
    
    builder.Services.AddSingleton(sp =>
        new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential(), serviceBusOptions));
}
else
{
    // Local development: Use connection string
    var connectionString = builder.Configuration["ServiceBus:ConnectionString"];
    if (!string.IsNullOrEmpty(connectionString))
    {
        builder.Services.AddSingleton(sp =>
            new ServiceBusClient(connectionString, new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpWebSockets
            }));
    }
}

// Configure Cosmos DB
// Priority: Managed Identity (production) > Connection String (if implemented for local dev)
var cosmosEndpoint = builder.Configuration["CosmosDb:Endpoint"];
var databaseName = builder.Configuration["CosmosDb:DatabaseName"];
var containerName = builder.Configuration["CosmosDb:ContainerName"];

if (!string.IsNullOrEmpty(cosmosEndpoint) && !string.IsNullOrEmpty(databaseName) && !string.IsNullOrEmpty(containerName))
{
    // Use managed identity for authentication
    builder.Services.AddSingleton(sp =>
    {
        var credential = new DefaultAzureCredential();
        var cosmosClient = new CosmosClient(cosmosEndpoint, credential);
        return cosmosClient;
    });
    
    builder.Services.AddSingleton(sp =>
    {
        var cosmosClient = sp.GetRequiredService<CosmosClient>();
        var database = cosmosClient.GetDatabase(databaseName);
        return database.GetContainer(containerName);
    });
}

// Configure Agent options
builder.Services.Configure<TravelPlanner.Shared.Services.AgentOptions>(builder.Configuration.GetSection("Agent"));

// Configure Agent Dashboard
var telemetryDir = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath(), "LogFiles");
builder.Services.AddAgentDashboard(options =>
{
    options.SharedFilePath = Path.Combine(telemetryDir, "agent-telemetry.jsonl");
    options.RegisteredAgents =
    [
        new AgentRegistration { Name = "Coordinator", Description = "Coordinates multi-agent workflow", AgentType = "CoordinatorAgent" },
        new AgentRegistration { Name = "CurrencyConverter", Description = "Currency conversion with real-time rates", AgentType = "CurrencyConverterAgent" },
        new AgentRegistration { Name = "WeatherAdvisor", Description = "Weather forecasts and packing advice", AgentType = "WeatherAdvisorAgent" },
        new AgentRegistration { Name = "LocalKnowledge", Description = "Local culture, safety, and tips", AgentType = "LocalKnowledgeAgent" },
        new AgentRegistration { Name = "ItineraryPlanner", Description = "Day-by-day itinerary creation", AgentType = "ItineraryPlannerAgent" },
        new AgentRegistration { Name = "BudgetOptimizer", Description = "Budget allocation and optimization", AgentType = "BudgetOptimizerAgent" },
    ];
    options.Topology = new AgentTopology
    {
        Phases =
        [
            new TopologyPhase { Name = "Information Gathering", Agents = ["CurrencyConverter", "WeatherAdvisor", "LocalKnowledge"], NextPhases = ["Itinerary Planning"] },
            new TopologyPhase { Name = "Itinerary Planning", Agents = ["ItineraryPlanner"], NextPhases = ["Budget Optimization"] },
            new TopologyPhase { Name = "Budget Optimization", Agents = ["BudgetOptimizer"], NextPhases = ["Final Assembly"] },
            new TopologyPhase { Name = "Final Assembly", Agents = ["Coordinator"] },
        ]
    };
});

// Configure Azure OpenAI Chat Client with dashboard instrumentation
builder.Services.AddChatClient(services =>
{
    var agentOptions = builder.Configuration.GetSection("Agent").Get<TravelPlanner.Shared.Services.AgentOptions>();
    var azureOpenAIEndpoint = agentOptions?.AzureOpenAIEndpoint;
    var modelDeploymentName = agentOptions?.ModelDeploymentName;
    
    if (azureOpenAIEndpoint == null || string.IsNullOrEmpty(modelDeploymentName))
    {
        throw new InvalidOperationException("Azure OpenAI endpoint and model deployment name must be configured");
    }
    
    var client = new AzureOpenAIClient(azureOpenAIEndpoint, new DefaultAzureCredential());
    return client.GetChatClient(modelDeploymentName).AsIChatClient();
}).UseAgentDashboard();

// Register HttpClient for external APIs
builder.Services.AddHttpClient<TravelPlanner.Shared.ExternalServices.IWeatherService, TravelPlanner.Shared.ExternalServices.NWSWeatherService>();
builder.Services.AddHttpClient<TravelPlanner.Shared.ExternalServices.ICurrencyService, TravelPlanner.Shared.ExternalServices.FrankfurterCurrencyService>();

// Register all specialized agents
builder.Services.AddScoped<TravelPlanner.Shared.Agents.CoordinatorAgent>();
builder.Services.AddScoped<TravelPlanner.Shared.Agents.CurrencyConverterAgent>();
builder.Services.AddScoped<TravelPlanner.Shared.Agents.WeatherAdvisorAgent>();
builder.Services.AddScoped<TravelPlanner.Shared.Agents.LocalKnowledgeAgent>();
builder.Services.AddScoped<TravelPlanner.Shared.Agents.ItineraryPlannerAgent>();
builder.Services.AddScoped<TravelPlanner.Shared.Agents.BudgetOptimizerAgent>();

// Register the multi-agent workflow orchestrator
builder.Services.AddScoped<TravelPlanner.Shared.Workflows.TravelPlanningWorkflow>();

// Register application services
builder.Services.AddScoped<ITravelPlanService, TravelPlanService>();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AllowAll");
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapAgentDashboard();

app.Run();
