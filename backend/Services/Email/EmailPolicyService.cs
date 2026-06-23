using System.Net.Mail;
using System.Reflection;
using DnsClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Arena.API.Services.Email;

public enum EmailRejectReason
{
    None = 0,
    Malformed,
    Disposable,
    Undeliverable,
}

/// <summary>Outcome of an email policy check. <see cref="Accepted"/> implies
/// <see cref="Normalized"/> is safe to persist and use.</summary>
public record EmailCheckResult(bool Accepted, string Normalized, EmailRejectReason Reason, string? Message)
{
    public static EmailCheckResult Ok(string normalized) =>
        new(true, normalized, EmailRejectReason.None, null);
    public static EmailCheckResult Reject(string normalized, EmailRejectReason reason, string message) =>
        new(false, normalized, reason, message);
}

/// <summary>
/// Single gate for "is this a safe, deliverable, non-throwaway address?". Runs at
/// signup. Layers: RFC format + normalization, disposable-domain blocklist, and a
/// best-effort DNS MX deliverability check (cached, configurable).
/// </summary>
public class EmailPolicyService
{
    private readonly EmailOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailPolicyService> _logger;
    private readonly HashSet<string> _disposableDomains;
    private readonly LookupClient _dns = new();

    public EmailPolicyService(
        IOptions<EmailOptions> options,
        IMemoryCache cache,
        IHostEnvironment env,
        ILogger<EmailPolicyService> logger)
    {
        _options = options.Value;
        _cache = cache;
        _env = env;
        _logger = logger;
        _disposableDomains = LoadDisposableDomains(_options.DisposableDomains);
    }

    /// <summary>Normalize an address to its canonical persisted form (trim + lower),
    /// or null if it isn't a syntactically valid single address.</summary>
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var trimmed = email.Trim();
        if (!MailAddress.TryCreate(trimmed, out var addr)) return null;
        // TryCreate accepts "Display <a@b>" forms; require the raw address only.
        if (!string.Equals(addr.Address, trimmed, StringComparison.OrdinalIgnoreCase)) return null;
        return addr.Address.ToLowerInvariant();
    }

    public async Task<EmailCheckResult> ValidateAsync(string? email, CancellationToken ct = default)
    {
        var normalized = Normalize(email);
        if (normalized is null)
            return EmailCheckResult.Reject(email?.Trim().ToLowerInvariant() ?? "",
                EmailRejectReason.Malformed, "Please enter a valid email address.");

        var domain = normalized[(normalized.IndexOf('@') + 1)..];

        if (_disposableDomains.Contains(domain))
            return EmailCheckResult.Reject(normalized, EmailRejectReason.Disposable,
                "Disposable email addresses aren't allowed. Please use a permanent address.");

        if (ShouldCheckMx() && !await HasMailExchangerAsync(domain, ct))
            return EmailCheckResult.Reject(normalized, EmailRejectReason.Undeliverable,
                "We couldn't verify that email domain can receive mail. Please check for typos.");

        return EmailCheckResult.Ok(normalized);
    }

    private bool ShouldCheckMx() => _options.CheckMx && !_env.IsDevelopment();

    private async Task<bool> HasMailExchangerAsync(string domain, CancellationToken ct)
    {
        var cacheKey = $"mx:{domain}";
        if (_cache.TryGetValue(cacheKey, out bool cached)) return cached;

        bool deliverable;
        try
        {
            var result = await _dns.QueryAsync(domain, QueryType.MX, cancellationToken: ct);
            // A domain with no MX but an A/AAAA record can still receive mail (implicit MX).
            deliverable = result.Answers.MxRecords().Any()
                || result.Answers.ARecords().Any()
                || result.Answers.AaaaRecords().Any();
            if (!deliverable)
            {
                var a = await _dns.QueryAsync(domain, QueryType.A, cancellationToken: ct);
                deliverable = a.Answers.ARecords().Any();
            }
        }
        catch (Exception ex)
        {
            // Fail open: a DNS hiccup shouldn't block a real signup.
            _logger.LogWarning(ex, "MX lookup failed for {Domain}; allowing through.", domain);
            deliverable = true;
        }

        _cache.Set(cacheKey, deliverable, TimeSpan.FromHours(deliverable ? 12 : 1));
        return deliverable;
    }

    private static HashSet<string> LoadDisposableDomains(IEnumerable<string> extra)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("disposable-domains.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is not null)
        {
            using var stream = asm.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var d = line.Trim();
                if (d.Length == 0 || d.StartsWith('#')) continue;
                set.Add(d.ToLowerInvariant());
            }
        }
        foreach (var d in extra)
            if (!string.IsNullOrWhiteSpace(d)) set.Add(d.Trim().ToLowerInvariant());
        return set;
    }
}
