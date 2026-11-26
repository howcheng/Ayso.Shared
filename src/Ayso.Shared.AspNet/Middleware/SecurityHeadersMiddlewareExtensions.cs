using Microsoft.AspNetCore.Builder;

namespace Ayso.Shared.AspNet.Middleware;

public static class SecurityHeadersMiddlewareExtensions
{
	public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseMiddleware<SecurityHeadersMiddleware>();
	}
}