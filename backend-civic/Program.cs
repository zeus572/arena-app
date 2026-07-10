using System.Text;
using Arena.Shared.Llm;
using Arena.Shared.News;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Civic.API.Data;
using Civic.API.Services;
using Civic.API.Services.Auth;
using Civic.API.Services.Campaign;
using Civic.API.Services.Generation;
using Civic.API.Services.Leagues;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISeedService, SeedService>();
builder.Services.AddSingleton<ICivicCatalog, CivicCatalog>();
builder.Services.AddScoped<IProfileScoringService, ProfileScoringService>();
builder.Services.AddScoped<IContradictionDetectionService, ContradictionDetectionService>();
builder.Services.AddScoped<IExplanationService, RuleBasedExplanationService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<IZeitgeistService, ZeitgeistService>();
builder.Services.AddScoped<ICohortService, CohortService>();

// News ingestion + civic content generation. Sources are typed descriptors
// (News:Sources / News:LocalSources) resolved through the shared provider
// registry from Arena.Shared — one fetch implementation across the monorepo.
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.Configure<NewsOptions>(builder.Configuration.GetSection("News"));

builder.Services.AddHttpClient<ILlmClient, ClaudeLlmClient>(c =>
{
    c.BaseAddress = new Uri("https://api.anthropic.com/");
    c.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddArenaNewsSources();

builder.Services.AddSingleton<INewsFeed>(sp =>
{
    var newsOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NewsOptions>>().Value;
    return sp.GetRequiredService<INewsSourceFactory>().CreateFeed(newsOpts.Sources);
});

builder.Services.AddHttpClient("RssNewsSource", c =>
{
    c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; CivicArenaBot/1.0)");
    c.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHostedService<NewsIngestionService>();
builder.Services.AddHostedService<CivicContentGenerationService>();

// Virtual Candidates: campaign post generation + reactions + matching.
builder.Services.Configure<CampaignOptions>(builder.Configuration.GetSection("Campaign"));
builder.Services.AddScoped<ICandidateSelectionService, CandidateSelectionService>();
builder.Services.AddScoped<ICandidateMatchService, CandidateMatchService>();
builder.Services.AddScoped<ICampaignReactionService, CampaignReactionService>();
builder.Services.AddSingleton<CampaignPostGenerationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CampaignPostGenerationService>());

// Campaign Manager game mode: manage an existing candidate to win their race.
builder.Services.Configure<CivicCampaignOptions>(builder.Configuration.GetSection("CivicCampaign"));
builder.Services.AddScoped<ICampaignPostFactory, CampaignPostFactory>();
builder.Services.AddScoped<CivicCampaignService>();

// Coalition game (Layer 0): provision birth + extraction.
builder.Services.AddScoped<Civic.API.Services.Coalition.ProvisionBirthService>();
builder.Services.AddScoped<
    Civic.API.Services.Coalition.IExtractionService,
    Civic.API.Services.Coalition.ExtractionService>();

// Coalition game (Layer 2/2H/3): the playable loop + seeding.
// SECURITY: the single LLM-access gate — only premium users trigger coalition LLM calls.
builder.Services.AddScoped<Civic.API.Services.Coalition.ILlmAccessPolicy, Civic.API.Services.Coalition.PremiumLlmAccessPolicy>();
builder.Services.AddScoped<Civic.API.Services.Coalition.ITwoFramingsService, Civic.API.Services.Coalition.TwoFramingsService>();
// Unified XP ledger: shared by the coalition loop AND campaign/reaction services so a
// player's reasoning XP reflects all engagement (not just coalition acts).
builder.Services.AddScoped<Civic.API.Services.Coalition.Product.ReasoningLedger>();
builder.Services.AddScoped<Civic.API.Services.Coalition.Product.CoalitionLoopService>();
builder.Services.AddScoped<Civic.API.Services.Coalition.Product.CoalitionSeeder>();
builder.Services.AddScoped<Civic.API.Services.Coalition.Judges.ICoalitionJudge, Civic.API.Services.Coalition.Judges.CoalitionJudge>();
builder.Services.AddScoped<Civic.API.Services.Coalition.Agents.IAgentProfileMapper, Civic.API.Services.Coalition.Agents.AgentProfileMapper>();
builder.Services.AddScoped<Civic.API.Services.Coalition.Product.CoalitionLifecycleService>();
builder.Services.AddHostedService<Civic.API.Services.Coalition.Product.CoalitionLifecycleHostedService>();

// ---- SocialPublisher (shared Arena.Shared.Social engine; civic content sources) ----
// Engine/platform/resilience knobs from "SocialPublisher"; civic selection knobs from "CivicSocial".
var civicSocialOptions = builder.Configuration.GetSection(Arena.Shared.Social.SocialPublisherOptions.SectionName)
    .Get<Arena.Shared.Social.SocialPublisherOptions>() ?? new Arena.Shared.Social.SocialPublisherOptions();
builder.Services.AddSingleton(civicSocialOptions);
builder.Services.AddSingleton(builder.Configuration.GetSection(Civic.API.Services.Social.CivicSocialOptions.SectionName)
    .Get<Civic.API.Services.Social.CivicSocialOptions>() ?? new Civic.API.Services.Social.CivicSocialOptions());
builder.Services.Configure<Arena.Shared.Social.Platforms.BlueskyOptions>(
    builder.Configuration.GetSection(Arena.Shared.Social.Platforms.BlueskyOptions.SectionName));

builder.Services.AddSingleton<Arena.Shared.Social.IClock, Arena.Shared.Social.SystemClock>();
builder.Services.AddSingleton<Arena.Shared.Social.Resilience.CircuitBreakerRegistry>();
builder.Services.AddSingleton<Arena.Shared.Social.Rendering.IHtmlRasterizer, Arena.Shared.Social.Rendering.SolidColorPngRasterizer>();
// Cards are drawn directly from the model (browser-free). HtmlCardRenderer + IHtmlRasterizer remain
// registered for the future headless-Chrome path; SkiaCardRenderer is the active renderer.
builder.Services.AddSingleton<Arena.Shared.Social.ICardRenderer, Arena.Shared.Social.Rendering.SkiaCardRenderer>();

builder.Services.AddHttpClient<Arena.Shared.Social.Platforms.BlueskyClient>();
builder.Services.AddSingleton<Arena.Shared.Social.IPlatformClient>(sp =>
    sp.GetRequiredService<Arena.Shared.Social.Platforms.BlueskyClient>());
builder.Services.AddSingleton<Arena.Shared.Social.IPlatformClientRegistry, Arena.Shared.Social.Platforms.PlatformClientRegistry>();

builder.Services.AddScoped<Arena.Shared.Social.ISocialPostStore>(sp =>
    new Arena.Shared.Social.EfSocialPostStore(sp.GetRequiredService<Civic.API.Data.CivicDbContext>()));
builder.Services.AddScoped<Arena.Shared.Social.IHighlightSelector, Civic.API.Services.Social.CivicHighlightSelector>();
builder.Services.AddScoped<Arena.Shared.Social.ISocialPublisher, Arena.Shared.Social.SocialPublisher>();
builder.Services.AddScoped<Arena.Shared.Social.SocialReviewService>();
builder.Services.AddScoped<Arena.Shared.Social.SocialHealthService>();
builder.Services.AddSingleton<Arena.Shared.Social.SocialHeartbeatHook>();

// Leagues: social competition groups (invites, membership, shared rounds, standings).
builder.Services.AddScoped<LeagueScoringService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<LeagueRoundService>();

// HTTP client for proxying premium-initiated debate creation to the debate
// backend. Base URL defaulted to the local dev port; override in production.
// Also used by BudgetFactsController to surface the debate backend's daily
// "Did You Know?" budget contradictions.
builder.Services.AddHttpClient("DebateApi", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Debate:ApiBaseUrl"] ?? "http://localhost:5000/");
    c.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "PoliticalArenaDevSecretKeyThatIsAtLeast32Characters!!")),
    };
});

// "VerifiedEmail" policy: authenticated AND email_verified=true. Gates account-bound
// Civic write/participation actions so unverified (throwaway) accounts can't spam them.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("VerifiedEmail", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new VerifiedEmailRequirement());
    });
});
builder.Services.AddSingleton<IAuthorizationHandler, VerifiedEmailHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, CivicAuthorizationResultHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Keep a couple of pooled connections warm so the first request after an idle
// stretch skips the cold Postgres connect — and, in prod, the managed-identity
// token handshake — which is a big chunk of the first-hit-per-endpoint latency
// on this low-traffic app. Set in code so it covers BOTH the dev path and the
// prod MI-datasource path without editing the (credential-bearing) prod secret;
// NpgsqlConnectionStringBuilder preserves the existing keys (Ssl Mode, Timeout,
// Trust Server Certificate, etc.).
connectionString = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
{
    MinPoolSize = 2,
}.ConnectionString;

if (builder.Environment.IsProduction())
{
    var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
    var credential = new DefaultAzureCredential();
    dataSourceBuilder.UsePeriodicPasswordProvider(async (_, ct) =>
    {
        var token = await credential.GetTokenAsync(
            new Azure.Core.TokenRequestContext(new[] { "https://ossrdbms-aad.database.windows.net/.default" }), ct);
        return token.Token;
    }, TimeSpan.FromMinutes(55), TimeSpan.FromSeconds(5));
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<CivicDbContext>(options =>
        options.UseNpgsql(dataSource));
}
else
{
    builder.Services.AddDbContext<CivicDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// "https://localhost" is the Capacitor Android WebView origin (the Civersify
// app serves its bundle from that scheme). It must be allowlisted wherever the
// mobile app talks to this API — including prod's Cors:Origins app setting.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5175", "https://localhost" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Run DB migration + seeding off the startup critical path (see
// InitializeDatabaseAsync) so Kestrel starts listening immediately, and gate
// request traffic on its completion via StartupReadiness.
builder.Services.AddSingleton<StartupReadiness>();
builder.Services.AddHostedService(sp => new DatabaseInitializerService(
    sp,
    sp.GetRequiredService<StartupReadiness>(),
    sp.GetRequiredService<ILogger<DatabaseInitializerService>>(),
    sp.GetRequiredService<IHostApplicationLifetime>(),
    InitializeDatabaseAsync));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();

// Explicit-bad-token guard. The AllowAnonymous endpoints resolve identity from
// the JWT and otherwise fall back to the shared "anonymous" id, returning
// 200-as-anonymous either way — so a client presenting an expired/invalid token
// gets silently downgraded with no signal to refresh. Here we make that failure
// loud: if a Bearer token is presented but did NOT authenticate, return 401.
// Requests with no Authorization header (genuine anonymous browsing, optionally
// via X-User-Id) are untouched, as are requests with a valid token.
app.Use(async (context, next) =>
{
    var auth = context.Request.Headers.Authorization.ToString();
    var presentedBearer = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(auth["Bearer ".Length..]);
    if (presentedBearer && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid or expired token." });
        return;
    }
    await next();
});

app.UseAuthorization();

// Readiness gate: until migration + seeding finishes, hold API traffic with a
// 503 + Retry-After so nothing is served against an un-migrated schema. /health
// is exempt so the platform warmup + health probes still pass while we init.
var startupReadiness = app.Services.GetRequiredService<StartupReadiness>();
app.Use(async (ctx, next) =>
{
    if (startupReadiness.IsReady || ctx.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var failed = startupReadiness.Status == StartupStatus.Failed;
    ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    ctx.Response.Headers.RetryAfter = failed ? "30" : "5";
    await ctx.Response.WriteAsJsonAsync(new
    {
        status = failed ? "unavailable" : "initializing",
        message = failed
            ? "Database initialization failed; the service is restarting."
            : "Service is starting up; please retry shortly.",
    });
});

app.MapControllers();

app.MapGet("/health", async (CivicDbContext db, StartupReadiness readiness) =>
{
    // While migrations run, report 200 "starting" (no DB access) so the platform
    // warmup probe passes and initialization gets a chance to finish. If init
    // failed, report 503 so the health check recycles the instance for a retry.
    if (readiness.Status == StartupStatus.Initializing)
        return Results.Ok(new { status = "starting", ready = false });

    if (readiness.Status == StartupStatus.Failed)
        return Results.Json(
            new { status = "unhealthy", ready = false, error = readiness.Error },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var petitionCount = await db.Petitions.CountAsync();
    return Results.Ok(new
    {
        status = "healthy",
        ready = true,
        timestamp = DateTime.UtcNow,
        petitionCount,
    });
});

app.Run();

// EF migrations + reference-data seeding. Invoked in the background by
// DatabaseInitializerService AFTER Kestrel starts listening, so the slow
// managed-identity → Postgres token handshake no longer blocks the container
// warmup probe. The readiness gate holds API traffic until this completes.
static async Task InitializeDatabaseAsync(IServiceProvider services, CancellationToken ct)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
    await db.Database.MigrateAsync(ct);

    var seeder = scope.ServiceProvider.GetRequiredService<ISeedService>();
    await seeder.SeedAsync();

    // Seed the coalition demo provisions (constructed agents; idempotent).
    var coalitionSeeder = scope.ServiceProvider.GetRequiredService<Civic.API.Services.Coalition.Product.CoalitionSeeder>();
    await coalitionSeeder.SeedAsync();

    // Pre-run the hottest read paths once, while the readiness gate still holds
    // traffic, so EF compiles their query shapes and the connection pool is warm
    // BEFORE the first real user. Without this, on a low-traffic app nearly every
    // page load is a cold "first hit" that pays 1-4s of JIT + EF query-shape
    // compilation (observed: races ~4s, feed ~1.7s, concepts ~2s). Best-effort:
    // warmup must never fail startup, so each step is isolated and swallowed.
    var warmupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await WarmupHotPathsAsync(scope.ServiceProvider, warmupLogger, ct);
}

// Exercises the read paths behind the slowest page loads so their query shapes
// and code paths are compiled ahead of the first request. Each step is isolated:
// a warmup failure is logged and ignored — it must never flip startup readiness
// to Failed (which would recycle the container).
static async Task WarmupHotPathsAsync(IServiceProvider scoped, ILogger logger, CancellationToken ct)
{
    async Task Step(string name, Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warmup step '{Step}' failed (non-fatal)", name);
        }
    }

    var db = scoped.GetRequiredService<CivicDbContext>();

    // /api/campaign-manager/races — the slowest cold first hit (~4s).
    await Step("races", () => scoped.GetRequiredService<CivicCampaignService>().GetRacesAsync(ct));

    // /api/zeitgeist — home strip.
    await Step("zeitgeist", () => scoped.GetRequiredService<IZeitgeistService>().BuildAsync(ct));

    // /api/campaign/feed (default recent, anonymous) — mirrors the controller's
    // Include(Fragments)+Include(Candidate) shape, which is what makes it heavy.
    await Step("campaign-feed", () => db.CampaignPosts
        .Include(p => p.Fragments)
        .Include(p => p.Candidate)
        .Where(p => p.OwnerUserId == null)
        .OrderByDescending(p => p.CreatedAt)
        .Take(20)
        .ToListAsync(ct));

    // /api/briefings (national page 1).
    await Step("briefings", () => db.Briefings
        .Where(b => b.Locality == null)
        .OrderBy(b => b.IssueOrder)
        .ThenByDescending(b => b.CreatedAt)
        .Take(20)
        .ToListAsync(ct));

    // /api/concepts.
    await Step("concepts", () => db.Concepts.OrderBy(c => c.Title).ToListAsync(ct));
}

// Exposed for WebApplicationFactory<Program> in Civic.ApiTests.
public partial class Program { }
