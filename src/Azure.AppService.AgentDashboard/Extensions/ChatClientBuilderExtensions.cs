using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.Extensions.AI;

namespace Azure.AppService.AgentDashboard.Extensions;

public static class ChatClientBuilderExtensions
{
    public static ChatClientBuilder UseAgentDashboard(
        this ChatClientBuilder builder,
        string agentName = "default")
    {
        return builder.Use((innerClient, services) =>
        {
            var store = services.GetRequiredService<AgentTelemetryStore>();
            return new InstrumentingChatClient(innerClient, store, agentName);
        });
    }

    private static T GetRequiredService<T>(this IServiceProvider services) where T : class
    {
        return services.GetService(typeof(T)) as T
            ?? throw new InvalidOperationException(
                $"Required service '{typeof(T).Name}' not found. Call services.AddAgentDashboard() before using UseAgentDashboard().");
    }
}
