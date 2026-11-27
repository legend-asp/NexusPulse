namespace NexusPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using NexusPulse.Application.Common.Models;
using NexusPulse.Infrastructure.Security;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    public const string SessionCookieName = "np_session";
    private readonly DashboardAuthOptions _authOptions;
    private readonly TotpService _totpService;

    public AuthController(IConfiguration configuration, TotpService totpService)
    {
        _authOptions = configuration.GetSection(DashboardAuthOptions.SectionName).Get<DashboardAuthOptions>() ?? new DashboardAuthOptions();
        _totpService = totpService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        if (!_authOptions.Enabled)
        {
            return Ok(new { enabled = false, authenticated = true, username = "operator" });
        }

        var token = Request.Cookies[SessionCookieName];
        bool isValid = _totpService.ValidateSessionToken(token, _authOptions.SessionSecret, out var username);

        return Ok(new
        {
            enabled = true,
            authenticated = isValid,
            username = isValid ? username : null
        });
    }

    public record LoginRequest(string? Username, string? Password, string TotpCode);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (!_authOptions.Enabled)
        {
            return Ok(new { authenticated = true, username = "operator" });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_totpService.IsRateLimited(clientIp, out int retryAfter))
        {
            Response.Headers.RetryAfter = retryAfter.ToString();
            return StatusCode(429, new { error = $"Too many failed attempts. Retry in {retryAfter}s." });
        }

        string expectedUser = string.IsNullOrWhiteSpace(_authOptions.Username) ? "admin" : _authOptions.Username;
        string providedUser = string.IsNullOrWhiteSpace(request.Username) ? expectedUser : request.Username.Trim();

        bool userValid = string.Equals(expectedUser, providedUser, StringComparison.OrdinalIgnoreCase);
        bool passwordValid = _totpService.ValidatePassword(_authOptions.Password, request.Password);
        bool totpValid = _totpService.ValidateTotpCode(_authOptions.TotpSecretBase32, request.TotpCode, expectedUser);

        if (!userValid || !passwordValid || !totpValid)
        {
            _totpService.RecordFailedAttempt(clientIp);
            return Unauthorized(new { error = "Invalid credentials or Google Authenticator code." });
        }

        _totpService.ClearFailedAttempts(clientIp);

        var lifetime = TimeSpan.FromHours(Math.Clamp(_authOptions.SessionLifetimeHours, 1, 168));
        var token = _totpService.CreateSessionToken(expectedUser, _authOptions.SessionSecret, lifetime);

        Response.Cookies.Append(SessionCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.Add(lifetime),
            Path = "/"
        });

        return Ok(new { authenticated = true, username = expectedUser });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(SessionCookieName, new CookieOptions { Path = "/" });
        return Ok(new { authenticated = false });
    }
}
