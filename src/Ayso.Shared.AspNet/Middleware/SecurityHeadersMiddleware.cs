using Ayso.Shared.AspNet.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ayso.Shared.AspNet.Middleware;

/// <summary>
/// Applies OWASP recommended HTTP security headers and a per-request nonce-based
/// <c>'strict-dynamic'</c> Content Security Policy.
/// </summary>
public class SecurityHeadersMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IWebHostEnvironment _environment;
	private readonly ILogger<SecurityHeadersMiddleware> _logger;

	public SecurityHeadersMiddleware(
		RequestDelegate next,
		IWebHostEnvironment environment,
		ILogger<SecurityHeadersMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(next);
		ArgumentNullException.ThrowIfNull(environment);
		ArgumentNullException.ThrowIfNull(logger);

		_next = next;
		_environment = environment;
		_logger = logger;
	}

	public Task InvokeAsync(HttpContext context, ICspNonceService cspNonceService)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(cspNonceService);

		var nonce = cspNonceService.GetNonce();

		context.Response.Headers["Content-Security-Policy"] = BuildCspPolicy(nonce);
		context.Response.Headers["X-Frame-Options"] = "DENY";
		context.Response.Headers["X-Content-Type-Options"] = "nosniff";
		context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
		context.Response.Headers["Permissions-Policy"] = _environment.IsDevelopment()
			? "camera=(), microphone=(), geolocation=(), unload=*"
			: "camera=(), microphone=(), geolocation=()";
		context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
		context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

		_logger.LogDebug("OWASP Security Headers and strict-dynamic CSP applied successfully.");

		return _next(context);
	}

	private string BuildCspPolicy(string nonce)
	{
		var connectSrc = _environment.IsDevelopment()
			? "connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*"
			: "connect-src 'self'";

		var directives = new[]
		{
			"default-src 'self'",
			$"script-src 'nonce-{nonce}' 'strict-dynamic'",
			"object-src 'none'",
			"style-src 'self'",
			"img-src 'self' data:",
			"font-src 'self'",
			connectSrc,
			"frame-ancestors 'none'",
			"base-uri 'self'",
			"form-action 'self'"
		};

		return string.Join("; ", directives);
	}
}