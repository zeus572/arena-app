using System.Text;
using Arena.Shared.Llm;
using Arena.Shared.News;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Civic.API.Data;
using Civic.API.Services;
using Civic.API.Services.Generation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISeedService, SeedService>();
builder.Services.AddSingleton<ICivicCatalog, CivicCatalog>();
builder.Services.AddScoped<IProfileScoringService, ProfileScoringService>();
builder.Services.AddScoped<IContradictionDetectionService, ContradictionDetectionService>();
builder.Services.AddScoped<IExplanationService, RuleBasedExplanationService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();

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

// HTTP client for proxying premium-initiated debate creation to the debate
// backend. Base URL defaulted to the local dev port; override in production.
builder.Services.AddHttpClient("DebateApi", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Debate:ApiBaseUrl"] ?? "http://localhost:5000/");
    c.Timeout = TimeSpan.FromSeconds(15);
});

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

builder.Services.AddAuthorization();

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
}

app.Run();

// Exposed for WebApplicationFactory<Program> in Civic.ApiTests.
public partial class Program { }
