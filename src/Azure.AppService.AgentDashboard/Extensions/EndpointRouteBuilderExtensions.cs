using Azure.AppService.AgentDashboard.Endpoints;
using Azure.AppService.AgentDashboard.Options;
using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.AspNetCore.Routing;

namespace Azure.AppService.AgentDashboard.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAgentDashboard(this IEndpointRouteBuilder endpoints)
    {
        var options = GetRequiredService<AgentDashboardOptions>(endpoints);
        var store = GetRequiredService<AgentTelemetryStore>(endpoints);

        AgentDashboardEndpoints.Map(endpoints, options, store);

        return endpoints;
    }

    private static T GetRequiredService<T>(IEndpointRouteBuilder endpoints) where T : class
    {
        return endpoints.ServiceProvider.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException(
                $"Required service '{typeof(T).Name}' not found. Call services.AddAgentDashboard() in your service configuration.");
    }
}
