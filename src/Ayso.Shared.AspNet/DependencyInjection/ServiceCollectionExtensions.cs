using Ayso.Shared.AspNet.Authorization;
using Ayso.Shared.AspNet.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ayso.Shared.AspNet.DependencyInjection;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers CSP nonce services and shared authorization requirement handlers.
	/// </summary>
	public static IServiceCollection AddAysoSharedAspNet(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddHttpContextAccessor();
		services.AddScoped<ICspNonceService, CspNonceService>();
		services.AddSingleton<IAuthorizationHandler, DomainRequirementHandler>();
		services.AddSingleton<IAuthorizationHandler, EmailAllowlistRequirementHandler>();

		return services;
	}
}