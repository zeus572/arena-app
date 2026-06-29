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

// News ingestion + civic content generation. Both backends register the
// same RssNewsSource pieces from Arena.Shared so the RSS fetch is one
// implementation across the monorepo.
builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.Configure<NewsOptions>(builder.Configuration.GetSection("News"));

builder.Services.AddHttpClient<ILlmClient, ClaudeLlmClient>(c =>
{
    c.BaseAddress = new Uri("https://api.anthropic.com/");
    c.Timeout = TimeSpan.FromSeconds(90);
});

builder.Services.AddSingleton<INewsFeed>(sp =>
{
    var newsOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NewsOptions>>().Value;
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    var sources = newsOpts.Sources
        .Select(kv => (INewsSource)new RssNewsSource(
            httpFactory.CreateClient("RssNewsSource"),
            kv.Key,
            new Uri(kv.Value),
            logger: loggerFactory.CreateLogger($"RssNewsSource[{kv.Key}]")))
        .ToList();

    return new AggregateNewsFeed(sources, loggerFactory.CreateLogger<AggregateNewsFeed>());
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
builder.Services.AddSingleton<Arena.Shared.Social.ICardRenderer, Arena.Shared.Social.Rendering.HtmlCardRenderer>();

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

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5175" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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
app.MapControllers();

app.MapGet("/health", async (CivicDbContext db) =>
{
    var petitionCount = await db.Petitions.CountAsync();
    return Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        petitionCount,
    });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CivicDbContext>();
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<ISeedService>();
    await seeder.SeedAsync();

    // Seed the coalition demo provisions (constructed agents; idempotent).
    var coalitionSeeder = scope.ServiceProvider.GetRequiredService<Civic.API.Services.Coalition.Product.CoalitionSeeder>();
    await coalitionSeeder.SeedAsync();
}

app.Run();

// Exposed for WebApplicationFactory<Program> in Civic.ApiTests.
public partial class Program { }
