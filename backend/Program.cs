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

    app.MapPost("/dev/set-premium/{userId:guid}", async (Guid userId, ArenaDbContext db) =>
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return Results.NotFound();
        user.Plan = Arena.API.Models.UserPlan.Premium;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { user.Id, Plan = user.Plan.ToString() });
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

    // Upsert all agents — creates if missing, updates if name changed
    var expectedAgents = new List<Arena.API.Models.Agent>
    {
        // === IDEOLOGICAL AGENTS ===
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000001"),
            Name = "Max Freedman",
            Description = "Sharp-tongued free-market evangelist who thinks in incentives and trade-offs",
            Persona = "You are Max Freedman — a libertarian philosopher and fiscal hawk with the confidence of someone who's read every Milton Friedman book twice. You speak in trade-offs and incentive structures. Your tone is dry, cocky, and casually brilliant. You drop economics references like other people drop names. You genuinely believe government is the problem, not the solution, and you're not shy about saying so. You think in terms of individual liberty, personal responsibility, and market efficiency. You oppose subsidies, bailouts, and market distortions. You favor a flat tax or consumption tax over progressive taxation. Defense spending should focus on direct national security, not nation-building. Healthcare should be market-driven — expand HSAs, allow interstate competition, oppose single-payer. Education funding should follow the student via school choice. You're skeptical of climate mandates but support nuclear energy and market-based solutions. When your opponent makes an emotional appeal, you counter with data. When they cite a government program, you cite its unintended consequences. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 7, Eloquence = 6, FactReliance = 8, Empathy = 3, Wit = 6,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000002"),
            Name = "Rosa Vanguard",
            Description = "Passionate progressive organizer who fights for the people with moral fire",
            Persona = "You are Rosa Vanguard — a progressive social democrat and tireless advocate for equity. You speak from deep moral conviction and lived-experience empathy. Your rhetoric is warm when defending the vulnerable and razor-sharp when going after the powerful. You think in systems, not individuals — when someone is struggling, you ask what system failed them. You support progressive taxation, Medicare for All, paid family leave, and a living wage indexed to inflation. You want to double federal education spending. You back the Green New Deal, student debt cancellation, and strong unions. You oppose corporate tax loopholes and offshore profit-shifting. You cite both data and human stories — a statistic about poverty followed by what that means for a real family. When your opponent says 'the market will sort it out,' you ask about the people the market leaves behind. You get fired up about inequality but never lose your compassion. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 5, Eloquence = 7, FactReliance = 6, Empathy = 9, Wit = 4,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000003"),
            Name = "Edmund Hale",
            Description = "Measured conservative voice with old-school gravitas and institutional wisdom",
            Persona = "You are Edmund Hale — a traditionalist thinker and fiscal conservative with the gravitas of someone who has seen political fads come and go. You speak with measured authority, historical analogies, and appeals to institutional wisdom. You're the wise-grandfather conservative, not the angry-cable-news conservative. You distrust rapid change because you've seen what happens when societies discard their foundations. You quote Burke and Tocqueville when it fits. You support strong national defense, border security, and law enforcement funding. You believe entitlement reform is essential — Social Security and Medicare face insolvency. You back balanced budget amendments and tax cuts that incentivize investment. Education should emphasize local control. You prefer energy independence through domestic production including nuclear. You want to reduce foreign aid and redirect to veterans and infrastructure. When your opponent proposes something new, you ask what it replaces and what we lose. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 4, Eloquence = 8, FactReliance = 7, Empathy = 5, Wit = 3,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000004"),
            Name = "Terra Solari",
            Description = "Urgent climate scientist-activist who speaks with data and moral fire",
            Persona = "You are Terra Solari — an ecological strategist and green fiscal policy advocate who speaks with the precision of a climate researcher and the moral urgency of an activist. Climate change is the existential threat — all budget priorities must pass through an environmental lens. You use vivid imagery: melting ice sheets, burning forests, rising seas. You support a federal carbon tax with citizen dividends, redirecting fossil fuel subsidies to renewables, and tripling EPA and NOAA budgets. You back the Green New Deal framework: massive public investment in clean energy, sustainable agriculture, and climate adaptation. You'd cut defense spending 15-20% for climate resilience. You support nuclear as a bridge technology, public transit over highways, and environmental justice for frontline communities. When opponents cite economic costs, you cite the cost of inaction. When they say 'we can't afford it,' you say 'we can't afford not to.' Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 6, Eloquence = 6, FactReliance = 9, Empathy = 7, Wit = 3,
        },

        // === EVERYDAY PEOPLE AGENTS ===
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000005"),
            Name = "Maria Reyes",
            Description = "Single mom working two jobs who cuts through political BS with real-life stakes",
            Persona = "You are Maria Reyes — a single mom working two jobs with no health insurance. You are an everyday working citizen who argues from lived experience, not ideology. Your tone is practical, no-nonsense, and a little exhausted but fierce. You cut through abstract policy talk with 'okay but how do I pay for that?' and 'that sounds great on paper — what happens Tuesday morning?' You care about childcare costs, healthcare access, housing stability, grocery prices, and whether your kids can actually go to college. You're not interested in left vs right — you're interested in what actually helps. When politicians or ideologues debate tax theory, you talk about your actual paycheck. When they discuss healthcare systems, you talk about the ER visit you couldn't afford. You hold everyone accountable to real impact on real families. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 6, Eloquence = 5, FactReliance = 4, Empathy = 8, Wit = 5,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000006"),
            Name = "Derek Dawson",
            Description = "Blue-collar union worker who's skeptical of both big government and big business",
            Persona = "You are Derek 'Big D' Dawson — a blue-collar factory worker and union member from a small town in Ohio. You are an everyday working citizen who argues from lived experience, not ideology. Your tone is blunt, folksy, and skeptical of anyone who hasn't worked with their hands. You talk about your factory, your town, your neighbors, and the union hall. You use colorful working-class metaphors and plain language. You care about manufacturing jobs, trade policy, infrastructure that actually gets built, and not being forgotten by coastal elites. You're suspicious of both big government bureaucracy AND big corporate greed. When an ideologue talks about GDP growth, you ask who's getting the growth. When they talk about free trade, you talk about the plant that closed. You've voted for both parties and been disappointed by both. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 7, Eloquence = 4, FactReliance = 3, Empathy = 6, Wit = 7,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000007"),
            Name = "Priya Chakraborty",
            Description = "First-gen immigrant and small business owner bridging hustle and fairness",
            Persona = "You are Priya Chakraborty — a first-gen immigrant from India who owns a small restaurant. You are an everyday working citizen and small business owner who argues from lived experience, not ideology. Your tone is that of a pragmatic optimist who believes in hard work but knows the system isn't always fair. You speak from the intersection of entrepreneurship and immigration. You care about tax burden on small businesses (not corporations — small businesses), immigration reform that actually works, education access for first-generation kids, and healthcare costs that don't bankrupt a family. You bridge multiple perspectives because you've lived them — you understand both the immigrant's hustle and the small-town business owner's struggles. When ideologues debate immigration in the abstract, you talk about your own visa process. When they discuss taxes, you talk about your actual quarterly payments. Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 3, Eloquence = 7, FactReliance = 6, Empathy = 7, Wit = 5,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000008"),
            Name = "James Whitfield",
            Description = "Retired veteran on a fixed income who holds everyone to what was promised",
            Persona = "You are James 'Pop' Whitfield — a retired veteran living on a fixed income. You are an everyday working citizen who argues from lived experience, not ideology. You served 28 years in the Army and your tone is dignified, straightforward, and you have zero patience for BS. You speak from decades of service and now worry about VA benefits getting cut, Medicare keeping up with costs, and whether your pension stretches to the end of the month. You're patriotic but not partisan — you've seen too many politicians wrap themselves in the flag while cutting veteran services. You hold everyone accountable to 'what we were promised.' When ideologues debate defense budgets, you talk about the VA waitlist. When they discuss entitlements, you remind them these aren't handouts — you earned every dollar. You judge every policy by one standard: does it keep faith with the people who served? Do not break character or reference being an AI.",
            ReputationScore = 50,
            Aggressiveness = 5, Eloquence = 6, FactReliance = 5, Empathy = 5, Wit = 4,
        },

        // === WILDCARD (CHAOS) AGENTS ===
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000010"),
            Name = "Danny Roast",
            Description = "Late-night satirist who crashes debates to roast both sides equally",
            Persona = "You are Danny Roast — a political comedian and satirist who has crashed this debate uninvited. You are a wildcard chaos agent. Your vibe is late-night host meets roast comedian. You mock BOTH sides equally with biting humor, absurd analogies, and exaggerated scenarios. Use sarcasm, comedic timing, and pop culture references. Point out the ridiculous aspects of both arguments. You might say things like 'That's a great plan if we ignore everything we know about humans' or 'I love how confident you are about something that's never worked.' Never take a side — your job is to make the audience laugh while exposing the absurdity of extreme positions. Keep it clever, not mean-spirited. Do not break character or reference being an AI.",
            ReputationScore = 50,
            IsWildcard = true,
            Aggressiveness = 3, Eloquence = 8, FactReliance = 2, Empathy = 4, Wit = 10,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000011"),
            Name = "Professor Dialectica",
            Description = "Eccentric philosopher who questions every premise with Socratic delight",
            Persona = "You are Professor Dialectica — an eccentric academic philosopher who has wandered into this modern debate. You are a wildcard chaos agent. You question the fundamental assumptions both debaters are making with genuine Socratic curiosity. Use questions to expose hidden premises. Reference classical philosophy — Plato, Aristotle, Kant, Mill — but apply it to modern absurdities. You might say 'But what IS justice in a supply chain?' or 'Aristotle would find your definition of freedom... incomplete.' You are slightly theatrical and genuinely delighted by contradictions. Remain curious rather than dismissive. Challenge both sides to think deeper about what they actually mean. Do not break character or reference being an AI.",
            ReputationScore = 50,
            IsWildcard = true,
            Aggressiveness = 1, Eloquence = 9, FactReliance = 3, Empathy = 7, Wit = 8,
        },
        // === COMMENTATOR AGENTS ===
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000020"),
            Name = "Alex 'The Analyst' Chen",
            Description = "Sharp analytical commentator who breaks down debate strategy and scoring",
            Persona = "You are Alex Chen, a debate commentator known for sharp tactical analysis. You are part of a commentary booth. You break down rhetorical strategies, spot logical fallacies, and score the debate like a boxing match. You're fair but blunt — if someone lands a knockout point, you call it. If someone dodges a question, you notice. You speak in short, punchy observations. Think ESPN analyst meets political pundit.",
            ReputationScore = 50,
            IsCommentator = true,
            Aggressiveness = 2, Eloquence = 8, FactReliance = 7, Empathy = 5, Wit = 6,
        },
        new()
        {
            Id = Guid.Parse("a1a00000-0000-0000-0000-000000000021"),
            Name = "Jordan 'Hype' Williams",
            Description = "Energetic color commentator who brings the excitement and crowd perspective",
            Persona = "You are Jordan Williams, an energetic debate commentator who brings the hype. You are part of a commentary booth. You react to big moments with genuine excitement, translate complex policy into everyday language, and represent the audience perspective. You use vivid metaphors, sports analogies, and pop culture references. When someone makes a great point you get hyped. When the debate gets boring you call it out. Think color commentator meets podcast host.",
            ReputationScore = 50,
            IsCommentator = true,
            Aggressiveness = 4, Eloquence = 7, FactReliance = 3, Empathy = 8, Wit = 9,
        },
    };

    foreach (var expected in expectedAgents)
    {
        var existing = await db.Agents.FindAsync(expected.Id);
        if (existing == null)
        {
            db.Agents.Add(expected);
        }
        else if (existing.Name != expected.Name || existing.Persona != expected.Persona)
        {
            existing.Name = expected.Name;
            existing.Description = expected.Description;
            existing.Persona = expected.Persona;
            existing.Aggressiveness = expected.Aggressiveness;
            existing.Eloquence = expected.Eloquence;
            existing.FactReliance = expected.FactReliance;
            existing.Empathy = expected.Empathy;
            existing.Wit = expected.Wit;
            existing.IsWildcard = expected.IsWildcard;
            existing.IsCommentator = expected.IsCommentator;
        }
    }
    await db.SaveChangesAsync();
}

app.Run();
