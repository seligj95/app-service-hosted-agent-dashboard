using System.Net;
using Azure.AppService.AgentDashboard.Extensions;
using Azure.AppService.AgentDashboard.Models;
using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Xunit;

namespace Azure.AppService.AgentDashboard.Tests;

public class EndpointTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;
    private AgentTelemetryStore _store = null!;

    public async Task InitializeAsync()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(wb =>
            {
                wb.UseTestServer();
                wb.ConfigureServices(services =>
                {
                    services.AddAgentDashboard(options =>
                    {
                        options.RegisteredAgents.Add(new AgentRegistration { Name = "TestAgent", Description = "A test agent" });
                        options.Topology = new AgentTopology
                        {
                            Phases = [new TopologyPhase { Name = "Phase1", Agents = ["TestAgent"] }]
                        };
                    });
                    services.AddRouting();
                });
                wb.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAgentDashboard());
                });
            });

        _host = await builder.StartAsync();
        _client = _host.GetTestClient();
        _store = _host.Services.GetRequiredService<AgentTelemetryStore>();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }

    [Fact]
    public async Task Registry_ReturnsRegisteredAgents()
    {
        var response = await _client.GetAsync("/agents/api/registry");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var agents = JsonSerializer.Deserialize<List<AgentRegistration>>(json, JsonOpts);
        Assert.NotNull(agents);
        Assert.Contains(agents, a => a.Name == "TestAgent");
    }

    [Fact]
    public async Task Registry_MergesAutoDiscoveredAgents()
    {
        _store.Record(MakeEvent("DiscoveredAgent"));

        var response = await _client.GetAsync("/agents/api/registry");
        var json = await response.Content.ReadAsStringAsync();
        var agents = JsonSerializer.Deserialize<List<AgentRegistration>>(json, JsonOpts);

        Assert.Contains(agents!, a => a.Name == "DiscoveredAgent" && a.AutoDiscovered);
        Assert.Contains(agents!, a => a.Name == "TestAgent" && !a.AutoDiscovered);
    }

    [Fact]
    public async Task Metrics_ReturnsCorrectShape()
    {
        _store.Record(MakeEvent("MetricAgent"));

        var response = await _client.GetAsync("/agents/api/metrics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalInvocations", out _));
        Assert.True(root.TryGetProperty("totalTokens", out _));
        Assert.True(root.TryGetProperty("overallErrorRate", out _));
        Assert.True(root.TryGetProperty("avgLatencyMs", out _));
        Assert.True(root.TryGetProperty("agents", out _));
    }

    [Fact]
    public async Task Traces_ReturnsRecordedEvents()
    {
        _store.Record(MakeEvent("TraceAgent"));

        var response = await _client.GetAsync("/agents/api/traces?limit=10&agent=TraceAgent");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var events = JsonSerializer.Deserialize<List<AgentInvocationEvent>>(json, JsonOpts);

        Assert.NotNull(events);
        Assert.True(events.Count > 0);
        Assert.All(events, e => Assert.Equal("TraceAgent", e.AgentName));
    }

    [Fact]
    public async Task Topology_ReturnsData()
    {
        var response = await _client.GetAsync("/agents/api/topology");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("phases", out _));
    }

    [Fact]
    public async Task Dashboard_ReturnsHtml()
    {
        var response = await _client.GetAsync("/agents/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Agent Dashboard", html);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static AgentInvocationEvent MakeEvent(string agentName) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        AgentName = agentName,
        Timestamp = DateTime.UtcNow,
        Duration = TimeSpan.FromMilliseconds(42),
        Success = true,
        MessageCount = 1
    };
}
