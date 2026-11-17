using Microsoft.EntityFrameworkCore;
using NexusPulse.Api.Hubs;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Infrastructure.BackgroundJobs;
using NexusPulse.Infrastructure.Persistence;
using NexusPulse.Infrastructure.Resilience;
using NexusPulse.Infrastructure.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers & OpenAPI / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 2. Real-time WebSockets (SignalR)
builder.Services.AddSignalR();

// 3. CORS for modern Frontend (React 19 + Vite on port 5173)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 4. Persistence (PostgreSQL 17 via EF Core 9)
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(defaultConnection);
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// 5. Distributed Cache & Locks (Redis)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6380";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false; // не крашить старт приложения, если Redis еще поднимается
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 6. Non-blocking In-Memory Event Channel (System.Threading.Channels)
builder.Services.AddSingleton<IIncidentChannel, IncidentChannel>();

// 7. Keyed Polly v8 Resilience Pipeline
builder.Services.AddNodeMonitoringResilience();

// 8. Resilient HttpClient with SocketsHttpHandler connection pooling
builder.Services.AddHttpClient("NodeMonitoringClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15), // Защита от socket exhaustion & DNS refresh
        ConnectTimeout = TimeSpan.FromSeconds(5)
    });

// 9. Background Telemetry Engine
builder.Services.AddHostedService<NodeHealthMonitoringWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowDashboard");

app.UseAuthorization();

app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");

app.Run();
