using Microsoft.EntityFrameworkCore;
using Arena.API.Data;
using Arena.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// LLM + Bot services
builder.Services.AddHttpClient<ILlmService, ClaudeLlmService>();
builder.Services.AddSingleton<TopicGeneratorService>();
builder.Services.AddSingleton<BudgetService>();
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

builder.Services.AddDbContext<ArenaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
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
app.UseAuthorization();
app.MapControllers();

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

app.Run();
