using System.Security.Claims;
using Ayso.Shared.AspNet.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Ayso.Shared.AspNet.Tests;

public class DomainRequirementHandlerTests
{
	private const string AllowedDomain = "aysoregion42.org";

	[Theory]
	[InlineData("director@aysoregion42.org")]
	[InlineData("Director@AYSORegion42.ORG")]
	public async Task HandleAsync_AllowedDomain_Succeeds(string email)
	{
		var requirement = new DomainRequirement(AllowedDomain);
		var handler = new DomainRequirementHandler();
		var context = new AuthorizationHandlerContext(
			[requirement],
			CreatePrincipal(true, new Claim(ClaimTypes.Email, email)),
			resource: null);

		await handler.HandleAsync(context);
		Assert.True(context.HasSucceeded);
	}

	[Theory]
	[InlineData("someone@gmail.com")]
	[InlineData("spoof@evil-aysoregion42.org")]
	public async Task HandleAsync_DisallowedDomain_DoesNotSucceed(string email)
	{
		var requirement = new DomainRequirement(AllowedDomain);
		var handler = new DomainRequirementHandler();
		var context = new AuthorizationHandlerContext(
			[requirement],
			CreatePrincipal(true, new Claim(ClaimTypes.Email, email)),
			resource: null);

		await handler.HandleAsync(context);
		Assert.False(context.HasSucceeded);
	}

	[Fact]
	public void DomainRequirement_EmptyDomain_Throws()
	{
		Assert.ThrowsAny<ArgumentException>(() => new DomainRequirement(" "));
	}

	private static ClaimsPrincipal CreatePrincipal(bool isAuthenticated, params Claim[] claims)
	{
		var identity = isAuthenticated
			? new ClaimsIdentity(claims, authenticationType: "Test")
			: new ClaimsIdentity(claims);
		return new ClaimsPrincipal(identity);
	}
}