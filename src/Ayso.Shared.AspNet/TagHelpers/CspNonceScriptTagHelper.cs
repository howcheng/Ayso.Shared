using Ayso.Shared.AspNet.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Ayso.Shared.AspNet.TagHelpers;

/// <summary>
/// Automatically adds the current request's Content Security Policy <c>nonce</c> attribute
/// to all Razor <c>&lt;script&gt;</c> elements so scripts execute under <c>script-src 'strict-dynamic'</c>.
/// Set <c>asp-add-nonce="false"</c> on an element to opt out.
/// </summary>
[HtmlTargetElement("script")]
public class CspNonceScriptTagHelper : TagHelper
{
	public const string NonceAttributeName = "nonce";

	private readonly ICspNonceService _cspNonceService;

	public CspNonceScriptTagHelper(ICspNonceService cspNonceService)
	{
		ArgumentNullException.ThrowIfNull(cspNonceService);
		_cspNonceService = cspNonceService;
	}

	/// <summary>
	/// Runs after built-in ASP.NET Core tag helpers so suppressed tags are skipped.
	/// </summary>
	public override int Order => 100;

	[HtmlAttributeName("asp-add-nonce")]
	public bool AddNonce { get; set; } = true;

	public override void Process(TagHelperContext context, TagHelperOutput output)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(output);

		if (output.TagName is null || !AddNonce || output.Attributes.ContainsName(NonceAttributeName))
		{
			return;
		}

		var nonce = _cspNonceService.GetNonce();
		if (!string.IsNullOrWhiteSpace(nonce))
		{
			output.Attributes.SetAttribute(NonceAttributeName, nonce);
		}
	}
}