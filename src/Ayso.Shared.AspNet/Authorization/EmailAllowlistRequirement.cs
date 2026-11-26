using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Ayso.Shared.AspNet.Authorization;

/// <summary>
/// Authorization requirement that validates the authenticated user's email against an allowlist.
/// Suitable for multi-region apps (e.g. Area10E) that are not limited to a single Google Workspace domain.
/// </summary>
public class EmailAllowlistRequirement : IAuthorizationRequirement
{
	public EmailAllowlistRequirement(IEnumerable<string> allowedEmails)
	{
		ArgumentNullException.ThrowIfNull(allowedEmails);

		AllowedEmails = allowedEmails
			.Where(e => !string.IsNullOrWhiteSpace(e))
			.Select(e => e.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		if (AllowedEmails.Count == 0)
		{
			throw new ArgumentException("At least one allowed email is required.", nameof(allowedEmails));
		}
	}

	public IReadOnlySet<string> AllowedEmails { get; }
}

/// <summary>
/// Authorization handler that succeeds when the authenticated user's email is in the allowlist.
/// </summary>
public class EmailAllowlistRequirementHandler : AuthorizationHandler<EmailAllowlistRequirement>
{
	protected override Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		EmailAllowlistRequirement requirement)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(requirement);

		if (context.User?.Identity?.IsAuthenticated != true)
		{
			return Task.CompletedTask;
		}

		var emailClaim = context.User.FindFirst(ClaimTypes.Email)
			?? context.User.FindFirst("email");

		if (emailClaim is null || string.IsNullOrWhiteSpace(emailClaim.Value))
		{
			return Task.CompletedTask;
		}

		if (requirement.AllowedEmails.Contains(emailClaim.Value.Trim()))
		{
			context.Succeed(requirement);
		}

		return Task.CompletedTask;
	}
}