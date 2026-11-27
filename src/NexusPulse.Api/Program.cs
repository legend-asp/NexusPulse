using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using NexusPulse.Api.Hubs;
using NexusPulse.Api.Middleware;
using NexusPulse.Api.Services;
using NexusPulse.Application.Common.Interfaces;
using NexusPulse.Infrastructure.BackgroundJobs;
using NexusPulse.Infrastructure.Persistence;
using NexusPulse.Infrastructure.Resilience;
using NexusPulse.Infrastructure.Security;
using NexusPulse.Infrastructure.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Controllers & OpenAPI / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 2. Real-time WebSockets (SignalR)
builder.Services.AddSignalR();
builder.Services.AddSingleton<IMetricsBroadcaster, MetricsBroadcaster>();

// 3. CORS for Frontend (same-origin Nginx proxy in prod, configurable origins, localhost Vite in dev)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 4. Persistence (PostgreSQL 17 via EF Core 9)
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(defaultConnection);
});
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// 5. Distributed Cache & Locks (Redis with in-memory fallback)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6380";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(redisConnectionString);
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// 6. Node Registry, Non-blocking Channel & 2FA TOTP Security
builder.Services.AddSingleton<IMonitoredNodeRegistry, MonitoredNodeRegistry>();
builder.Services.AddSingleton<IIncidentChannel, IncidentChannel>();
builder.Services.AddSingleton<TotpService>();

// 7. Keyed Polly v8 Resilience Pipeline
builder.Services.AddNodeMonitoringResilience();

// 8. HttpClients (Standard + Self-Signed TLS client for raw-IP admin panels, IPv4 preferred)
static async ValueTask<Stream> ConnectPreferIpv4Async(SocketsHttpConnectionContext context, CancellationToken ct)
{
    var host = context.DnsEndPoint.Host;
    var port = context.DnsEndPoint.Port;

    IPAddress[] addresses;
    if (IPAddress.TryParse(host, out var parsed))
    {
        addresses = [parsed];
    }
    else
    {
        var all = await Dns.GetHostAddressesAsync(host, ct);
        var ipv4 = all.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToArray();
        addresses = ipv4.Length > 0 ? ipv4 : all;
    }

    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
    try
    {
        await socket.ConnectAsync(addresses, port, ct);
        return new NetworkStream(socket, ownsSocket: true);
    }
    catch
    {
        socket.Dispose();
        throw;
    }
}

builder.Services.AddHttpClient("NodeMonitoringClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        ConnectTimeout = TimeSpan.FromSeconds(8),
        AllowAutoRedirect = false,
        ConnectCallback = ConnectPreferIpv4Async
    });

builder.Services.AddHttpClient("InsecureNodeMonitoringClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        ConnectTimeout = TimeSpan.FromSeconds(8),
        AllowAutoRedirect = false,
        ConnectCallback = ConnectPreferIpv4Async,
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true
        }
    });

builder.Services.AddHttpClient("GeminiClient");

// 9. AI & Telegram Alert Dispatching Services
builder.Services.AddScoped<IAiDiagnosticService, GeminiDiagnosticService>();
builder.Services.AddScoped<ITelegramNotifier, TelegramBotNotifier>();

// 10. Background Telemetry & Incident Processing Engines
builder.Services.AddHostedService<NodeHealthMonitoringWorker>();
builder.Services.AddHostedService<IncidentProcessingWorker>();
builder.Services.AddHostedService<TelegramPollingWorker>();

var app = builder.Build();

// Apply pending EF Core migrations gracefully if PostgreSQL is online
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning("Database migration skipped (running in resilient in-memory mode): {Message}", ex.Message);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders();
app.UseCors("AllowDashboard");
app.UseMiddleware<DashboardAuthMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", utc = DateTime.UtcNow }));
app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");

app.Run();
