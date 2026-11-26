using Microsoft.AspNetCore.Builder;

namespace Ayso.Shared.AspNet.Middleware;

public static class ThemeCookieMiddlewareExtensions
{
	public static IApplicationBuilder UseThemeCookie(this IApplicationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.UseMiddleware<ThemeCookieMiddleware>();
	}
}