using System.Text;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Arena.API.Data;
using Arena.API.Services;
using Arena.API.Services.FactProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddMemoryCache();

// Fact-checking providers
builder.Services.AddHttpClient<UsaFactsProvider>();
builder.Services.AddHttpClient<WikipediaProvider>();
builder.Services.AddHttpClient<WebSearchProvider>();
builder.Services.AddHttpClient<BudgetDataProvider>();
builder.Services.AddTransient<IFactProvider, UsaFactsProvider>();
builder.Services.AddTransient<IFactProvider, WikipediaProvider>();
builder.Services.AddTransient<IFactProvider, WebSearchProvider>();
builder.Services.AddTransient<IFactProvider, BudgetDataProvider>();
builder.Services.AddTransient<FactCheckService>();
builder.Services.AddScoped<TaggingService>();
builder.Services.AddScoped<JwtTokenService>();

// Authentication
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
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? "DevSecretKeyThatIsAtLeast32CharsLong!!")),
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Premium", policy =>
        policy.RequireClaim("plan", "Premium"));
    options.AddPolicy("Admin", policy =>
        policy.RequireAssertion(context =>
        {
            var email = context.User.FindFirst("email")?.Value;
            var adminEmails = builder.Configuration.GetSection("Auth:AdminEmails").Get<string[]>() ?? [];
            return email is not null && adminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
        }));
});

// LLM + Bot services
builder.Services.AddHttpClient<ILlmService, ClaudeLlmService>();
builder.Services.AddSingleton<TopicGeneratorService>();
builder.Services.AddScoped<NewsTopicService>();
builder.Services.AddScoped<TopicModerationService>();
builder.Services.AddHostedService<DailyTopicRefreshService>();
builder.Services.AddSingleton<BudgetService>();
builder.Services.AddSingleton(new HeartbeatSettings
{
    Enabled = builder.Configuration.GetValue("BotHeartbeat:Enabled", true),
    IntervalSeconds = builder.Configuration.GetValue("BotHeartbeat:IntervalSeconds", 900),
});
builder.Services.AddHostedService<BotHeartbeatService>();

// Ranking services
builder.Services.AddSingleton<RankingService>();
builder.Services.AddHostedService<RankingRollupService>();

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

    builder.Services.AddDbContext<ArenaDbContext>(options =>
        options.UseNpgsql(dataSource));
}
else
{
    builder.Services.AddDbContext<ArenaDbContext>(options =>
        options.UseNpgsql(connectionString));
}

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:5174" };

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

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/tick", async (IServiceProvider sp) =>
    {
        var heartbeat = sp.GetServices<IHostedService>()
            .OfType<BotHeartbeatService>()
            .FirstOrDefault();
        if (heartbeat is null) return Results.Problem("BotHeartbeatService not found");
        await heartbeat.RunHeartbeatAsync(CancellationToken.None);
        return Results.Ok(new { status = "tick complete", timestamp = DateTime.UtcNow });
    }).RequireCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    app.MapPost("/dev/backfill-tags", async (ArenaDbContext db, TaggingService tagging) =>
    {
        await tagging.BackfillAllAsync(db);
        var count = await db.Tags.CountAsync();
        return Results.Ok(new { status = "backfill complete", tagCount = count });
    }).RequireCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    app.MapPost("/dev/generate-news-topics", async (NewsTopicService news) =>
    {
        await news.GenerateTopicsFromNewsAsync();
        return Results.Ok(new { status = "news topic generation complete", timestamp = DateTime.UtcNow });
    }).RequireCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
}

app.MapGet("/health", async (ArenaDbContext db) =>
{
    var activeDebates = await db.Debates.CountAsync(d => d.Status == Arena.API.Models.DebateStatus.Active);
    var totalDebates = await db.Debates.CountAsync();
    var totalTurns = await db.Turns.CountAsync();
    return Results.Ok(new
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        activeDebates,
        totalDebates,
        totalTurns,
    });
});

// Apply pending migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ArenaDbContext>();
    await db.Database.MigrateAsync();

    var topicService = scope.ServiceProvider.GetRequiredService<TopicGeneratorService>();
    await topicService.SeedStaticTopicsAsync();
}

app.Run();
