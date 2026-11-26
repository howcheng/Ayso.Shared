using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ayso.Shared.AspNet.Authentication;

/// <summary>
/// Registers the shared cookie authentication scheme. External providers (Google, Microsoft, etc.)
/// remain the responsibility of each host application's <c>Program.cs</c>.
/// </summary>
public static class AysoCookieAuthenticationExtensions
{
	public static AuthenticationBuilder AddAysoCookieAuthentication(
		this IServiceCollection services,
		Action<CookieAuthenticationOptions>? configureCookie = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services
			.AddAuthentication(options =>
			{
				options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
			})
			.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
			{
				options.Cookie.Name = "ayso.auth";
				options.Cookie.HttpOnly = true;
				options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
				options.Cookie.SameSite = SameSiteMode.Lax;
				options.SlidingExpiration = true;
				configureCookie?.Invoke(options);
			});
	}
}