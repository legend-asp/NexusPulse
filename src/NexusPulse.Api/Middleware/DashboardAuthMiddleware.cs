namespace NexusPulse.Api.Middleware;

using NexusPulse.Api.Controllers;
using NexusPulse.Application.Common.Models;
using NexusPulse.Infrastructure.Security;

public sealed class DashboardAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DashboardAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration, TotpService totpService)
    {
        var authOptions = configuration.GetSection(DashboardAuthOptions.SectionName).Get<DashboardAuthOptions>();
        if (authOptions == null || !authOptions.Enabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        bool isProtectedPath = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                               path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase);

        if (!isProtectedPath)
        {
            await _next(context);
            return;
        }

        var token = context.Request.Cookies[AuthController.SessionCookieName];
        if (!totpService.ValidateSessionToken(token, authOptions.SessionSecret, out _))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Google Authenticator 2FA session required.\"}");
            return;
        }

        await _next(context);
    }
}
