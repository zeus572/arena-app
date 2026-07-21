using System.Net;
using Arena.Shared.Llm;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Arena.Shared.Tests;

public class GptLlmClientTests
{
    private record Demo(string Headline, int Rank);

    private static OpenAiOptions Opts() => new()
    {
        ApiKey = "test-key",
        SonnetBackupModel = "gpt-5.6-terra",
        HaikuBackupModel = "gpt-5.6-luna",
        DefaultMaxTokens = 1024,
    };

    private static string OpenAiBody(string assistantText) =>
        $$"""
        {
          "id": "chatcmpl_test",
          "object": "chat.completion",
          "model": "gpt-5.6-terra",
          "choices": [
            {
              "index": 0,
              "message": { "role": "assistant", "content": {{System.Text.Json.JsonSerializer.Serialize(assistantText)}} },
              "finish_reason": "stop"
            }
          ]
        }
        """;

    [Fact]
    public async Task GenerateStructured_HappyPath_ReturnsDeserializedObject()
    {
        var assistantJson = "{\"headline\":\"Bill advances\",\"rank\":1}";
        var handler = StubHttpMessageHandler.FromBody(OpenAiBody(assistantJson));
        var http = new HttpClient(handler);

        var client = new GptLlmClient(http, Options.Create(Opts()));
        var result = await client.GenerateStructuredAsync<Demo>("sys", "user");

        result.Headline.Should().Be("Bill advances");
        result.Rank.Should().Be(1);
        handler.Requests.Should().HaveCount(1);
        handler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task GenerateStructured_StripsFencesAndParses()
    {
        var fenced = "```json\n{\"headline\":\"x\",\"rank\":2}\n```";
        var http = new HttpClient(StubHttpMessageHandler.FromBody(OpenAiBody(fenced)));
        var client = new GptLlmClient(http, Options.Create(Opts()));

        var result = await client.GenerateStructuredAsync<Demo>("sys", "user");
        result.Rank.Should().Be(2);
    }

    [Fact]
    public async Task GenerateStructured_RetriesOnceWhenJsonFails_ThenThrowsBadResponse()
    {
        var calls = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            calls++;
            return Task.FromResult((HttpStatusCode.OK, OpenAiBody("this is not json"), "application/json"));
        });
        var http = new HttpClient(handler);

        var client = new GptLlmClient(http, Options.Create(Opts()));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        var ex = await act.Should().ThrowAsync<LlmException>();
        calls.Should().Be(2, "the client should retry exactly once before giving up");
        ex.Which.Kind.Should().Be(LlmFailureKind.BadResponse);
    }

    [Fact]
    public async Task GenerateStructured_NonSuccessStatus_ThrowsCallFailedWithRawBody()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult((HttpStatusCode.TooManyRequests, "{\"error\":\"rate limited\"}", "application/json")));
        var http = new HttpClient(handler);

        var client = new GptLlmClient(http, Options.Create(Opts()));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.RawResponse.Should().Contain("rate limited");
        ex.Which.Kind.Should().Be(LlmFailureKind.CallFailed);
    }

    [Fact]
    public async Task GenerateStructured_MissingApiKey_ThrowsUnavailable()
    {
        var http = new HttpClient(StubHttpMessageHandler.FromBody(OpenAiBody("{}")));
        var opts = Opts();
        opts.ApiKey = "";

        var client = new GptLlmClient(http, Options.Create(opts));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        var ex = await act.Should().ThrowAsync<LlmException>().WithMessage("*ApiKey*");
        ex.Which.Kind.Should().Be(LlmFailureKind.Unavailable);
    }

    [Fact]
    public async Task GenerateStructured_Disabled_ThrowsUnavailable_WithoutCalling()
    {
        var handler = StubHttpMessageHandler.FromBody(OpenAiBody("{}"));
        var http = new HttpClient(handler);
        var opts = Opts();
        opts.Enabled = false;

        var client = new GptLlmClient(http, Options.Create(opts));
        var act = async () => await client.GenerateStructuredAsync<Demo>("sys", "user");

        var ex = await act.Should().ThrowAsync<LlmException>();
        ex.Which.Kind.Should().Be(LlmFailureKind.Unavailable);
        handler.Requests.Should().BeEmpty("a disabled provider must not hit the network");
    }

    [Fact]
    public async Task GenerateStructured_PicksBackupModelByTier()
    {
        var handler = StubHttpMessageHandler.FromBody(OpenAiBody("{\"headline\":\"h\",\"rank\":1}"));
        var http = new HttpClient(handler);
        var client = new GptLlmClient(http, Options.Create(Opts()));

        await client.GenerateStructuredAsync<Demo>("sys", "user", LlmModelTier.Haiku);

        handler.RequestBodies.Should().HaveCount(1);
        handler.RequestBodies[0].Should().Contain("gpt-5.6-luna");
        handler.RequestBodies[0].Should().Contain("max_completion_tokens");
    }
}
