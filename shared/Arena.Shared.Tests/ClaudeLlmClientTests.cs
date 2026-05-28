using System.Net;
using Arena.Shared.Llm;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.Shared.Tests;

public class ClaudeLlmClientTests
{
    private record Demo(string Headline, int Rank);

    private static AnthropicOptions Opts() => new()
    {
        ApiKey = "test-key",
        SonnetModel = "claude-sonnet-4-6",
        HaikuModel = "claude-haiku-4-5",
        DefaultMaxTokens = 1024,
    };

    private static string AnthropicBody(string assistantText) =>
        $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "model": "claude-sonnet-4-6",
          "content": [
            { "type": "text", "text": {{System.Text.Json.JsonSerializer.Serialize(assistantText)}} }
          ],
          "stop_reason": "end_turn"
        }
        """;

    [Fact]
    public async Task GenerateStructured_HappyPath_ReturnsDeserializedObject()
    {
        var assistantJson = "{\"headline\":\"Bill advances\",\"rank\":1}";
        var handler = StubHttpMessageHandler.FromBody(AnthropicBody(assistantJson));
        var http = new HttpClient(handler);

        var client = new ClaudeLlmClient(http, Options.Create(Opts()));
        var result = await client.GenerateStructuredAsync<Demo>("sys", "user");

        result.Headline.Should().Be("Bill advances");
        result.Rank.Should().Be(1);
        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Headers.GetValues("x-api-key").Should().Contain("test-key");
        handler.Requests[0].Headers.GetValues("anthropic-version").Should().Contain("2023-06-01");
    }

    [Fact]
    public async Task GenerateStructured_StripsFencesAndParses()
    {
        var fenced = "```json\n{\"headline\":\"x\",\"rank\":2}\n```";
        var http = new HttpClient(StubHttpMessageHandler.FromBody(AnthropicBody(fenced)));
        var client = new ClaudeLlmClient(http, Options.Create(Opts()));

        var result = await client.GenerateStructuredAsync<Demo>("sys", "user");
        result.Rank.Should().Be(2);
    }

    [Fact]
    public async Task GenerateStructured_RetriesOnceWhenJsonFails_ThenThrows()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            calls++;
            return Task.FromResult((HttpStatusCode.OK, AnthropicBody("this is not json"), "application/json"));
        });
        var http = new HttpClient(handler);

        var client = new ClaudeLlmClient(http, Options.Create(Opts()));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        await act.Should().ThrowAsync<LlmException>();
        calls.Should().Be(2, "the client should retry exactly once before giving up");
    }

    [Fact]
    public async Task GenerateStructured_RetrySucceedsAfterInitialBadJson()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            calls++;
            var body = calls == 1
                ? AnthropicBody("not json at all")
                : AnthropicBody("{\"headline\":\"recovered\",\"rank\":3}");
            return Task.FromResult((HttpStatusCode.OK, body, "application/json"));
        });
        var http = new HttpClient(handler);

        var client = new ClaudeLlmClient(http, Options.Create(Opts()));
        var result = await client.GenerateStructuredAsync<Demo>("sys", "user");

        result.Rank.Should().Be(3);
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GenerateStructured_NonSuccessStatus_ThrowsWithRawBody()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult((HttpStatusCode.BadRequest, "{\"error\":\"bad\"}", "application/json")));
        var http = new HttpClient(handler);

        var client = new ClaudeLlmClient(http, Options.Create(Opts()));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.RawResponse.Should().Contain("bad");
    }

    [Fact]
    public async Task GenerateStructured_MissingApiKey_Throws()
    {
        var handler = StubHttpMessageHandler.FromBody(AnthropicBody("{}"));
        var http = new HttpClient(handler);
        var opts = Opts();
        opts.ApiKey = "";

        var client = new ClaudeLlmClient(http, Options.Create(opts));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        await act.Should().ThrowAsync<LlmException>()
            .WithMessage("*ApiKey*");
    }

    [Fact]
    public async Task GenerateStructured_PicksModelByTier()
    {
        var handler = StubHttpMessageHandler.FromBody(AnthropicBody("{\"headline\":\"h\",\"rank\":1}"));
        var http = new HttpClient(handler);
        var client = new ClaudeLlmClient(http, Options.Create(Opts()));

        await client.GenerateStructuredAsync<Demo>("sys", "user", LlmModelTier.Haiku);

        handler.RequestBodies.Should().HaveCount(1);
        handler.RequestBodies[0].Should().Contain("claude-haiku-4-5");
    }
}
