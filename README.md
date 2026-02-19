# Azure App Service Agent Dashboard

An observability dashboard for .NET AI agent applications running on Azure App Service. Instrument your `IChatClient` calls with a single middleware and get a real-time web dashboard showing per-agent metrics, traces, topology, and token usage — no external services required.

## Repository structure

```
├── src/Azure.AppService.AgentDashboard/   # The NuGet package
├── tests/                                 # Package unit tests
└── demo-app/                              # Example: MAF Travel Planner with dashboard integrated
```

| Folder | Description |
|---|---|
| [`src/Azure.AppService.AgentDashboard`](src/Azure.AppService.AgentDashboard/) | The dashboard package — IChatClient middleware, telemetry store, JSON API, and embedded HTML dashboard. See the [package README](src/Azure.AppService.AgentDashboard/README.md) for full documentation. |
| [`tests`](tests/) | Unit tests for the package (18 tests). |
| [`demo-app`](demo-app/app-service-maf-openai-travel-agent-dotnet/) | A multi-agent travel planner built with Microsoft Agent Framework (MAF) and Azure OpenAI, with the dashboard fully integrated. Deployable to Azure App Service via `azd up`. |

## Getting the package

### Option 1: Project reference (recommended for trying it out)

Clone this repo and reference the package project directly from your app:

```shell
git clone https://github.com/seligj95/app-service-hosted-agent-dashboard.git
```

In your `.csproj`:

```xml
<ProjectReference Include="path/to/app-service-hosted-agent-dashboard/src/Azure.AppService.AgentDashboard/Azure.AppService.AgentDashboard.csproj" />
```

### Option 2: Build a local NuGet package

If you want to consume it as a NuGet package without publishing to a feed:

```shell
cd hosted-agent-dashboard
dotnet pack src/Azure.AppService.AgentDashboard -c Release -o ./nupkgs
```

Then add the local folder as a NuGet source and install:

```shell
# Add local source (one-time)
dotnet nuget add source ./nupkgs --name local

# Install in your project
dotnet add package Azure.AppService.AgentDashboard --source local
```

### Option 3: Copy the source folder

The package is self-contained in `src/Azure.AppService.AgentDashboard/` with a single external dependency (`Microsoft.Extensions.AI`). You can copy that folder into your solution and add a project reference — no other files from this repo are needed.

## Quick start

```csharp
using Azure.AppService.AgentDashboard.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 1. Register dashboard services
builder.Services.AddAgentDashboard();

// 2. Add the instrumentation middleware to your IChatClient
builder.Services.AddChatClient(/* your IChatClient */).UseAgentDashboard();

var app = builder.Build();

// 3. Map the dashboard endpoints
app.MapAgentDashboard();

app.Run();
```

Then open `/agents/dashboard` in your browser.

See the [package README](src/Azure.AppService.AgentDashboard/README.md) for full documentation on agent tagging, topology, cross-process telemetry, configuration options, and API endpoints.

## Running the demo app

The demo app is a multi-agent travel planner that uses 6 agents (Coordinator, CurrencyConverter, WeatherAdvisor, LocalKnowledge, ItineraryPlanner, BudgetOptimizer) orchestrated across phases.

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- An Azure subscription

### Deploy

```shell
cd demo-app/app-service-maf-openai-travel-agent-dotnet
azd up
```

This provisions an App Service, Azure OpenAI (gpt-4o), Service Bus, and Cosmos DB, then deploys the API and WebJob. The dashboard is available at `https://<your-app>.azurewebsites.net/agents/dashboard`.

## Running the tests

```shell
dotnet test
```

## License

MIT
