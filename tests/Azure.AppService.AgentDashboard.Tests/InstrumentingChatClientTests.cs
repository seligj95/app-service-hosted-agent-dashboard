using Azure.AppService.AgentDashboard.Models;
using Azure.AppService.AgentDashboard.Telemetry;
using Microsoft.Extensions.AI;
using Xunit;

namespace Azure.AppService.AgentDashboard.Tests;

public class InstrumentingChatClientTests
{
    private readonly AgentTelemetryStore _store = new();

    [Fact]
    public async Task GetResponseAsync_RecordsSuccessEvent()
    {
        var expectedUsage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20, TotalTokenCount = 30 };
        var innerResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "hello")])
        {
            ModelId = "gpt-4o",
            Usage = expectedUsage
        };
        var inner = new FakeChatClient(innerResponse);
        var client = new InstrumentingChatClient(inner, _store, "TestAgent");

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };
        var response = await client.GetResponseAsync(messages);

        Assert.Equal("hello", response.Text);

        var events = _store.GetRecentEvents(10);
        Assert.Single(events);

        var evt = events[0];
        Assert.Equal("TestAgent", evt.AgentName);
        Assert.True(evt.Success);
        Assert.Null(evt.ErrorMessage);
        Assert.Equal("gpt-4o", evt.ModelId);
        Assert.Equal(10, evt.InputTokens);
        Assert.Equal(20, evt.OutputTokens);
        Assert.Equal(30, evt.TotalTokens);
        Assert.True(evt.Duration.TotalMilliseconds >= 0);
        Assert.Equal(1, evt.MessageCount);
    }

    [Fact]
    public async Task GetResponseAsync_RecordsErrorEvent()
    {
        var inner = new FakeChatClient(new InvalidOperationException("LLM failed"));
        var client = new InstrumentingChatClient(inner, _store, "FailAgent");

        var messages = new List<ChatMessage> { new(ChatRole.User, "test") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(messages));

        var events = _store.GetRecentEvents(10);
        Assert.Single(events);

        var evt = events[0];
        Assert.Equal("FailAgent", evt.AgentName);
        Assert.False(evt.Success);
        Assert.Equal("LLM failed", evt.ErrorMessage);
    }

    [Fact]
    public async Task GetResponseAsync_UsesAgentNameFromChatOptions()
    {
        var inner = new FakeChatClient(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));
        var client = new InstrumentingChatClient(inner, _store, "DefaultName");

        var options = new ChatOptions();
        options.AdditionalProperties ??= [];
        options.AdditionalProperties["AgentName"] = "OverrideName";

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "test")], options);

        var evt = _store.GetRecentEvents(1)[0];
        Assert.Equal("OverrideName", evt.AgentName);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_RecordsEventAfterEnumeration()
    {
        var updates = new List<ChatResponseUpdate>
        {
            new() { Contents = [new TextContent("hel")], ModelId = "gpt-4o" },
            new() { Contents = [new TextContent("lo")] }
        };
        var inner = new FakeChatClient(updates);
        var client = new InstrumentingChatClient(inner, _store, "StreamAgent");

        var messages = new List<ChatMessage> { new(ChatRole.User, "hi") };
        var collected = new List<string>();

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            collected.Add(update.Text ?? "");
        }

        Assert.Equal(["hel", "lo"], collected);

        var events = _store.GetRecentEvents(10);
        Assert.Single(events);

        var evt = events[0];
        Assert.Equal("StreamAgent", evt.AgentName);
        Assert.True(evt.Success);
        Assert.Equal("gpt-4o", evt.ModelId);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_RecordsErrorOnFault()
    {
        var inner = FakeChatClient.WithStreamException(new IOException("stream broken"));
        var client = new InstrumentingChatClient(inner, _store, "StreamFail");

        var messages = new List<ChatMessage> { new(ChatRole.User, "test") };

        await Assert.ThrowsAsync<IOException>(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(messages)) { }
        });

        var evt = _store.GetRecentEvents(1)[0];
        Assert.False(evt.Success);
        Assert.Equal("stream broken", evt.ErrorMessage);
    }

    private sealed class FakeChatClient : IChatClient
    {
        private readonly ChatResponse? _response;
        private readonly Exception? _exception;
        private readonly List<ChatResponseUpdate>? _updates;
        private Exception? _streamException;

        public FakeChatClient(ChatResponse response) => _response = response;
        public FakeChatClient(Exception exception) => _exception = exception;
        public FakeChatClient(List<ChatResponseUpdate> updates) => _updates = updates;

        public static FakeChatClient WithStreamException(Exception streamException)
        {
            return new FakeChatClient { _streamException = streamException };
        }

        private FakeChatClient() { }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null) throw _exception;
            return Task.FromResult(_response!);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_streamException is not null)
            {
                await Task.CompletedTask;
                throw _streamException;
            }

            if (_updates is not null)
            {
                foreach (var u in _updates)
                {
                    await Task.Yield();
                    yield return u;
                }
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
