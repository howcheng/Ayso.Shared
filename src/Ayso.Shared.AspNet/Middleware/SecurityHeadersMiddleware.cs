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
	private static readonly string[] DevConnectBaseSources =
	[
		"'self'",
		"ws://localhost:*",
		"wss://localhost:*",
		"http://localhost:*",
		"https://localhost:*"
	];

	private static readonly string[] ProdConnectBaseSources = ["'self'"];
	private static readonly string[] ImageBaseSources = ["'self'", "data:"];
	private static readonly string[] FrameBaseSources = ["'self'"];

	private readonly RequestDelegate _next;
	private readonly IWebHostEnvironment _environment;
	private readonly ILogger<SecurityHeadersMiddleware> _logger;
	private readonly Func<HttpContext, string?>? _coopResolver;
	private readonly string _defaultCoop;
	private readonly string[] _additionalConnectSources;
	private readonly string[] _additionalFrameSources;
	private readonly string[] _additionalImageSources;

	public SecurityHeadersMiddleware(
		RequestDelegate next,
		IWebHostEnvironment environment,
		ILogger<SecurityHeadersMiddleware> logger,
		SecurityHeadersOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(next);
		ArgumentNullException.ThrowIfNull(environment);
		ArgumentNullException.ThrowIfNull(logger);

		var resolvedOptions = options ?? new SecurityHeadersOptions();
		ArgumentException.ThrowIfNullOrWhiteSpace(resolvedOptions.CrossOriginOpenerPolicy);

		_next = next;
		_environment = environment;
		_logger = logger;
		_coopResolver = resolvedOptions.CrossOriginOpenerPolicyResolver;
		_defaultCoop = ValidateHeaderValue(resolvedOptions.CrossOriginOpenerPolicy, nameof(SecurityHeadersOptions.CrossOriginOpenerPolicy));
		_additionalConnectSources = NormalizeSources(resolvedOptions.AdditionalConnectSources, nameof(SecurityHeadersOptions.AdditionalConnectSources));
		_additionalFrameSources = NormalizeSources(resolvedOptions.AdditionalFrameSources, nameof(SecurityHeadersOptions.AdditionalFrameSources));
		_additionalImageSources = NormalizeSources(resolvedOptions.AdditionalImageSources, nameof(SecurityHeadersOptions.AdditionalImageSources));
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
		context.Response.Headers["Cross-Origin-Opener-Policy"] = ResolveCrossOriginOpenerPolicy(context);
		context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";

		_logger.LogDebug("OWASP Security Headers and strict-dynamic CSP applied successfully.");

		return _next(context);
	}

	private string BuildCspPolicy(string nonce)
	{
		var connectBaseSources = _environment.IsDevelopment()
			? DevConnectBaseSources
			: ProdConnectBaseSources;

		var directives = new List<string>(11)
		{
			"default-src 'self'",
			$"script-src 'nonce-{nonce}' 'strict-dynamic'",
			"object-src 'none'",
			"style-src 'self'",
			BuildDirective("img-src", ImageBaseSources, _additionalImageSources),
			"font-src 'self'",
			BuildDirective("connect-src", connectBaseSources, _additionalConnectSources)
		};

		if (_additionalFrameSources.Length > 0)
		{
			directives.Add(BuildDirective("frame-src", FrameBaseSources, _additionalFrameSources));
		}

		directives.Add("frame-ancestors 'none'");
		directives.Add("base-uri 'self'");
		directives.Add("form-action 'self'");

		return string.Join("; ", directives);
	}

	private static string BuildDirective(string directiveName, string[] baseSources, string[] additionalSources)
	{
		if (additionalSources.Length == 0)
		{
			return $"{directiveName} {string.Join(' ', baseSources)}";
		}

		var combined = new List<string>(baseSources.Length + additionalSources.Length);
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var source in baseSources)
		{
			if (seen.Add(source))
			{
				combined.Add(source);
			}
		}

		foreach (var source in additionalSources)
		{
			if (seen.Add(source))
			{
				combined.Add(source);
			}
		}

		return $"{directiveName} {string.Join(' ', combined)}";
	}

	private string ResolveCrossOriginOpenerPolicy(HttpContext context)
	{
		var customPolicy = _coopResolver?.Invoke(context);
		if (string.IsNullOrWhiteSpace(customPolicy))
		{
			return _defaultCoop;
		}

		return ValidateHeaderValue(customPolicy, nameof(SecurityHeadersOptions.CrossOriginOpenerPolicyResolver));
	}

	private static string[] NormalizeSources(IList<string> sources, string paramName)
	{
		if (sources.Count == 0)
		{
			return [];
		}

		var normalized = new List<string>(sources.Count);
		foreach (var source in sources)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				continue;
			}

			var trimmed = source.Trim();
			if (trimmed.IndexOfAny([';', '\r', '\n']) >= 0)
			{
				throw new ArgumentException($"CSP source '{trimmed}' contains invalid directive separator characters.", paramName);
			}

			normalized.Add(trimmed);
		}

		return [.. normalized];
	}

	private static string ValidateHeaderValue(string value, string paramName)
	{
		var trimmed = value.Trim();
		if (trimmed.IndexOfAny(['\r', '\n', ';']) >= 0)
		{
			throw new ArgumentException($"Header value '{trimmed}' contains invalid characters.", paramName);
		}

		return trimmed;
	}
}
