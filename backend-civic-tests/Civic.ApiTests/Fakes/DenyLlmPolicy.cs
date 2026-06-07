using Arena.Shared.Llm;
using Civic.API.Services.Coalition;

namespace Civic.ApiTests.Fakes;

/// <summary>An LLM access policy that denies everyone — simulates an anonymous/non-premium request.</summary>
public sealed class DenyLlmPolicy : ILlmAccessPolicy
{
    public bool CanUseLlm() => false;
    public void EnsureAllowed() => throw new LlmException("LLM access requires a premium account.");
}
