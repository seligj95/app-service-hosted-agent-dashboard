using System.Reflection;
using System.Text.Json;
using Azure.AppService.AgentDashboard.Models;
using Azure.AppService.AgentDashboard.Options;
using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Azure.AppService.AgentDashboard.Endpoints;

public static class AgentDashboardEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Map(IEndpointRouteBuilder endpoints, AgentDashboardOptions options, AgentTelemetryStore store)
    {
        var prefix = options.RoutePrefix.TrimEnd('/');

        endpoints.MapGet($"{prefix}/api/registry", () =>
        {
            var registered = options.RegisteredAgents.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
            var discovered = store.GetAgentNames();

            foreach (var name in discovered)
            {
                if (!registered.ContainsKey(name))
                {
                    registered[name] = new AgentRegistration
                    {
                        Name = name,
                        AutoDiscovered = true
                    };
                }
            }

            return Results.Json(registered.Values.ToList(), JsonOptions);
        });

        endpoints.MapGet($"{prefix}/api/metrics", () =>
        {
            var metrics = store.GetMetrics();
            var summary = new
            {
                uptime = store.UptimeSince,
                totalInvocations = metrics.Sum(m => m.InvocationCount),
                totalTokens = metrics.Sum(m => m.TotalTokens),
                overallErrorRate = metrics.Sum(m => m.InvocationCount) > 0
                    ? Math.Round((double)metrics.Sum(m => m.ErrorCount) / metrics.Sum(m => m.InvocationCount), 4)
                    : 0,
                avgLatencyMs = metrics.Count > 0
                    ? Math.Round(metrics.Average(m => m.AvgLatencyMs), 2)
                    : 0,
                agents = metrics
            };
            return Results.Json(summary, JsonOptions);
        });

        endpoints.MapGet($"{prefix}/api/traces", (int? limit, string? agent) =>
        {
            var clampedLimit = Math.Clamp(limit ?? 50, 1, 500);
            var events = store.GetRecentEvents(clampedLimit, agent);
            return Results.Json(events, JsonOptions);
        });

        endpoints.MapGet($"{prefix}/api/topology", () =>
        {
            if (options.Topology is not null)
            {
                return Results.Json(options.Topology, JsonOptions);
            }

            // Auto-generate flat topology from discovered agents
            var agentNames = store.GetAgentNames();
            var topology = new AgentTopology
            {
                Phases = agentNames.Count > 0
                    ? [new TopologyPhase { Name = "Agents", Agents = agentNames }]
                    : []
            };
            return Results.Json(topology, JsonOptions);
        });

        endpoints.MapGet($"{prefix}/dashboard", () =>
        {
            var assembly = typeof(AgentDashboardEndpoints).Assembly;
            var stream = assembly.GetManifestResourceStream("Azure.AppService.AgentDashboard.Resources.dashboard.html");

            if (stream is null)
            {
                return Results.NotFound("Dashboard resource not found.");
            }

            return Results.Stream(stream, "text/html");
        });
    }
}
