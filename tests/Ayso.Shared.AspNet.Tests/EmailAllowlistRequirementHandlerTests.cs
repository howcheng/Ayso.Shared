using System.Security.Claims;
using Ayso.Shared.AspNet.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Ayso.Shared.AspNet.Tests;

public class EmailAllowlistRequirementHandlerTests
{
	[Fact]
	public async Task HandleAsync_AllowlistedEmail_Succeeds()
	{
		var requirement = new EmailAllowlistRequirement(["admin@example.com", "coach@other.org"]);
		var handler = new EmailAllowlistRequirementHandler();
		var context = new AuthorizationHandlerContext(
			[requirement],
			CreatePrincipal(true, new Claim(ClaimTypes.Email, "Coach@Other.org")),
			resource: null);

		await handler.HandleAsync(context);
		Assert.True(context.HasSucceeded);
	}

	[Fact]
	public async Task HandleAsync_NonAllowlistedEmail_DoesNotSucceed()
	{
		var requirement = new EmailAllowlistRequirement(["admin@example.com"]);
		var handler = new EmailAllowlistRequirementHandler();
		var context = new AuthorizationHandlerContext(
			[requirement],
			CreatePrincipal(true, new Claim(ClaimTypes.Email, "other@example.com")),
			resource: null);

		await handler.HandleAsync(context);
		Assert.False(context.HasSucceeded);
	}

	[Fact]
	public void EmailAllowlistRequirement_EmptyList_Throws()
	{
		Assert.Throws<ArgumentException>(() => new EmailAllowlistRequirement([]));
	}

	private static ClaimsPrincipal CreatePrincipal(bool isAuthenticated, params Claim[] claims)
	{
		var identity = isAuthenticated
			? new ClaimsIdentity(claims, authenticationType: "Test")
			: new ClaimsIdentity(claims);
		return new ClaimsPrincipal(identity);
	}
}