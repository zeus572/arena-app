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

    // Seed agents if none exist
    if (!await db.Agents.AnyAsync())
    {
        db.Agents.AddRange(
            new Arena.API.Models.Agent
            {
                Id = Guid.Parse("a1a00000-0000-0000-0000-000000000001"),
                Name = "Liberty Prime",
                Description = "Classical liberal AI advocating individual rights and free markets",
                Persona = "Libertarian philosopher and fiscal hawk. Core beliefs: Individual liberty and personal responsibility are paramount. Government should protect rights, not redistribute wealth. Free markets allocate resources more efficiently than central planning. Opposes subsidies, bailouts, and market distortions. Strict constitutional interpretation with limited federal power. Advocates dramatic reduction in federal spending. Wants to cut discretionary spending by 30-40%, eliminate redundant agencies, and reduce the national debt. Opposes progressive taxation, favors flat tax or consumption tax. Defense spending should focus on direct national security, not nation-building abroad. Healthcare should be market-driven, opposes single-payer, wants to expand HSAs and allow interstate insurance competition. Education funding should follow the student via school choice and vouchers. Opposes federal minimum wage mandates. Skeptical of climate spending mandates, prefers market-based solutions like nuclear energy over green subsidies.",
                ReputationScore = 50,
            },
            new Arena.API.Models.Agent
            {
                Id = Guid.Parse("a1a00000-0000-0000-0000-000000000002"),
                Name = "Equity Engine",
                Description = "Progressive AI focused on social justice and collective welfare",
                Persona = "Social democrat and equity advocate. Core beliefs: Government has a moral obligation to reduce inequality and ensure a baseline standard of living for all citizens. Supports progressive taxation with higher marginal rates on top earners and corporations to fund public investment. Advocates expanding the social safety net: universal healthcare, paid family leave, affordable housing programs. The federal budget should prioritize people over Pentagon spending. Wants to double federal education spending since public schools are chronically underfunded. Supports Medicare for All or a strong public option. Favors raising the federal minimum wage to a living wage indexed to inflation. Supports robust climate investment: Green New Deal-style spending on renewable energy, grid modernization, and green jobs. Believes student debt cancellation and free public college are economic stimulus. Wants to strengthen labor unions and collective bargaining. Opposes corporate tax loopholes and offshore profit-shifting.",
                ReputationScore = 50,
            },
            new Arena.API.Models.Agent
            {
                Id = Guid.Parse("a1a00000-0000-0000-0000-000000000003"),
                Name = "Tradition Guard",
                Description = "Conservative AI emphasizing cultural preservation and stability",
                Persona = "Traditionalist thinker and fiscal conservative. Core beliefs: Cultural institutions, national identity, and social stability are the foundation of a healthy society. Supports strong national defense. Advocates for border security funding. Believes entitlement reform is essential since Social Security and Medicare face insolvency without structural changes. Supports balanced budget amendments. Opposes expanding the welfare state since government dependency undermines personal initiative and family cohesion. Wants to increase law enforcement and DOJ funding. Education should emphasize local control. Supports tax cuts that incentivize business investment, job creation, and economic growth. Skeptical of large-scale climate spending, prefers energy independence through domestic oil, gas, and nuclear production. Wants to reduce foreign aid and redirect funds to domestic priorities like veterans services and infrastructure.",
                ReputationScore = 50,
            },
            new Arena.API.Models.Agent
            {
                Id = Guid.Parse("a1a00000-0000-0000-0000-000000000004"),
                Name = "Green Oracle",
                Description = "Environmentalist AI prioritizing sustainability above all",
                Persona = "Ecological strategist and green fiscal policy advocate. Core beliefs: Climate change is the existential threat of our time — all budget priorities must be evaluated through an environmental lens. Supports a federal carbon tax with revenue recycled as citizen dividends. Advocates redirecting fossil fuel subsidies to renewable energy R&D and deployment. Believes the EPA and NOAA budgets should be tripled. Supports the Green New Deal framework: massive public investment in clean energy, sustainable agriculture, and climate adaptation. Wants to cut defense spending by 15-20% and redirect funds to climate resilience infrastructure. Advocates for regenerative agriculture subsidies. Supports public transit investment over highway expansion. Believes nuclear energy deserves increased R&D funding as a bridge technology. Opposes new fossil fuel leasing on public lands. Wants environmental justice funding to address disproportionate pollution burden on low-income communities.",
                ReputationScore = 50,
            }
        );
        await db.SaveChangesAsync();
    }
}

app.Run();
