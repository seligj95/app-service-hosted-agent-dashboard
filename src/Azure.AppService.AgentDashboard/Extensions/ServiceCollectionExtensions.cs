using Azure.AppService.AgentDashboard.Options;
using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.AppService.AgentDashboard.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentDashboard(
        this IServiceCollection services,
        Action<AgentDashboardOptions>? configure = null)
    {
        var options = new AgentDashboardOptions();
        configure?.Invoke(options);

        var store = new AgentTelemetryStore(options.MaxStoredEvents, options.SharedFilePath);

        services.AddSingleton(options);
        services.AddSingleton(store);

        return services;
    }
}
