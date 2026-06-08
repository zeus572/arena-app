using Arena.Shared.Llm;
using Microsoft.Extensions.Hosting;

namespace Civic.API.Services.Coalition;

/// <summary>
/// SECURITY / COST GATE. Decides whether the CURRENT request is allowed to trigger an
/// LLM call from the coalition feature. The rule: only an authenticated PREMIUM user
/// may directly trigger coalition LLM calls — anonymous and free users always get the
/// heuristic fallbacks (no LLM cost, no abuse vector). This is the single chokepoint:
/// every coalition LLM seam (judge, extraction, birth, agent-mapper, two-framings)
/// consults it, so there is no path for an anon/non-premium user to spend LLM budget
/// outside the app's normal (separately-gated) turn flow.
/// </summary>
public interface ILlmAccessPolicy
{
    bool CanUseLlm();

    /// <summary>Throws <see cref="LlmException"/> when not allowed, so callers fall back uniformly.</summary>
    void EnsureAllowed();
}

public class PremiumLlmAccessPolicy : ILlmAccessPolicy
{
    private readonly IHttpContextAccessor _http;
    private readonly IHostEnvironment _env;

    public PremiumLlmAccessPolicy(IHttpContextAccessor http, IHostEnvironment env)
    {
        _http = http;
        _env = env;
    }

    public bool CanUseLlm()
    {
        // DEV-ONLY override: in Development any caller may use the LLM (with a key set) so the
        // coalition flow can be exercised interactively without a Premium JWT. The Premium gate
        // below remains the only path in Production.
        if (_env.IsDevelopment()) return true;

        var ctx = _http.HttpContext;
        // No request context = a trusted background/system caller (e.g. the lifecycle
        // scheduler birthing provisions). Those may use the LLM; the rule below targets
        // IN-REQUEST users only.
        if (ctx is null) return true;

        var user = ctx.User;
        if (user.Identity?.IsAuthenticated != true) return false;   // anonymous (X-User-Id only) -> no LLM
        var plan = user.FindFirst("plan")?.Value;                   // JWT plan claim, as in DebateInitController
        return string.Equals(plan, "Premium", StringComparison.OrdinalIgnoreCase);
    }

    public void EnsureAllowed()
    {
        if (!CanUseLlm())
            throw new LlmException("LLM access requires a premium account.");
    }
}
