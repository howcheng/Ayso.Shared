using Microsoft.AspNetCore.Builder;

namespace Ayso.Shared.AspNet.Middleware;

public static class SecurityHeadersMiddlewareExtensions
{
	public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
	{
		return UseSecurityHeaders(builder, configure: null);
	}

	public static IApplicationBuilder UseSecurityHeaders(
		this IApplicationBuilder builder,
		Action<SecurityHeadersOptions>? configure)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new SecurityHeadersOptions();
		configure?.Invoke(options);

		return builder.UseMiddleware<SecurityHeadersMiddleware>(options);
	}
}
