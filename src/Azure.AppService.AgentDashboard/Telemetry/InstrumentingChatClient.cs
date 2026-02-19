using System.Diagnostics;
using System.Runtime.CompilerServices;
using Azure.AppService.AgentDashboard.Models;
using Microsoft.Extensions.AI;

namespace Azure.AppService.AgentDashboard.Telemetry;

public sealed class InstrumentingChatClient : DelegatingChatClient
{
    private readonly AgentTelemetryStore _store;
    private readonly string _defaultAgentName;

    public InstrumentingChatClient(IChatClient innerClient, AgentTelemetryStore store, string defaultAgentName = "default")
        : base(innerClient)
    {
        _store = store;
        _defaultAgentName = defaultAgentName;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var agentName = ResolveAgentName(options);
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await base.GetResponseAsync(messageList, options, cancellationToken);
            sw.Stop();

            var toolsUsed = ExtractToolNames(response);

            _store.Record(new AgentInvocationEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                AgentName = agentName,
                Timestamp = DateTime.UtcNow,
                Duration = sw.Elapsed,
                Success = true,
                ModelId = response.ModelId ?? options?.ModelId,
                InputTokens = response.Usage?.InputTokenCount,
                OutputTokens = response.Usage?.OutputTokenCount,
                TotalTokens = response.Usage?.TotalTokenCount,
                MessageCount = messageList.Count,
                ToolsUsed = toolsUsed
            });

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            _store.Record(new AgentInvocationEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                AgentName = agentName,
                Timestamp = DateTime.UtcNow,
                Duration = sw.Elapsed,
                Success = false,
                ErrorMessage = ex.Message,
                ModelId = options?.ModelId,
                MessageCount = messageList.Count
            });

            throw;
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var agentName = ResolveAgentName(options);
        var messageList = messages as IList<ChatMessage> ?? messages.ToList();
        var sw = Stopwatch.StartNew();
        string? modelId = options?.ModelId;
        var updates = new List<ChatResponseUpdate>();
        Exception? caughtException = null;

        IAsyncEnumerator<ChatResponseUpdate> enumerator =
            base.GetStreamingResponseAsync(messageList, options, cancellationToken).GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                ChatResponseUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                        break;
                    update = enumerator.Current;
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                    throw;
                }

                updates.Add(update);
                modelId ??= update.ModelId;
                yield return update;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
            sw.Stop();

            var toolsUsed = updates
                .SelectMany(u => u.Contents?.OfType<FunctionCallContent>() ?? [])
                .Select(fc => fc.Name)
                .Where(n => n is not null)
                .Distinct()
                .Cast<string>()
                .ToList();

            _store.Record(new AgentInvocationEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                AgentName = agentName,
                Timestamp = DateTime.UtcNow,
                Duration = sw.Elapsed,
                Success = caughtException is null,
                ErrorMessage = caughtException?.Message,
                ModelId = modelId,
                MessageCount = messageList.Count,
                ToolsUsed = toolsUsed.Count > 0 ? toolsUsed : null
            });
        }
    }

    private string ResolveAgentName(ChatOptions? options)
    {
        if (options?.AdditionalProperties is not null &&
            options.AdditionalProperties.TryGetValue("AgentName", out var nameObj) &&
            nameObj is string name &&
            !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return _defaultAgentName;
    }

    private static List<string>? ExtractToolNames(ChatResponse response)
    {
        var tools = response.Messages
            .SelectMany(m => m.Contents?.OfType<FunctionCallContent>() ?? [])
            .Select(fc => fc.Name)
            .Where(n => n is not null)
            .Distinct()
            .Cast<string>()
            .ToList();

        return tools.Count > 0 ? tools : null;
    }
}
