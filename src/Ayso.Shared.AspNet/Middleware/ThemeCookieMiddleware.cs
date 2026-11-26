using Microsoft.AspNetCore.Http;

namespace Ayso.Shared.AspNet.Middleware;

/// <summary>
/// Refreshes the theme preference cookie's expiration on every request (sliding expiration).
/// </summary>
public class ThemeCookieMiddleware
{
	public const string ThemeCookieName = "ayso_theme";
	public static readonly TimeSpan ThemeCookieLifetime = TimeSpan.FromDays(400);

	private readonly RequestDelegate _next;

	public ThemeCookieMiddleware(RequestDelegate next)
	{
		ArgumentNullException.ThrowIfNull(next);
		_next = next;
	}

	public Task InvokeAsync(HttpContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		if (context.Request.Cookies.TryGetValue(ThemeCookieName, out var themeValue) &&
			!string.IsNullOrEmpty(themeValue))
		{
			context.Response.Cookies.Append(ThemeCookieName, themeValue, new CookieOptions
			{
				Expires = DateTimeOffset.UtcNow.Add(ThemeCookieLifetime),
				HttpOnly = false,
				IsEssential = false,
				SameSite = SameSiteMode.Lax,
				Secure = context.Request.IsHttps,
				Path = "/"
			});
		}

		return _next(context);
	}
}