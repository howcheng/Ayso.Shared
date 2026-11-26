using Ayso.Shared.AspNet.Services;
using Ayso.Shared.AspNet.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Ayso.Shared.AspNet.Tests;

public class CspNonceScriptTagHelperTests
{
	[Fact]
	public void Process_AddsNonceAttribute()
	{
		var nonceService = new FixedNonceService("abc123");
		var helper = new CspNonceScriptTagHelper(nonceService);
		var context = new TagHelperContext(
			tagName: "script",
			allAttributes: new TagHelperAttributeList(),
			items: new Dictionary<object, object>(),
			uniqueId: "test");
		var output = new TagHelperOutput(
			"script",
			new TagHelperAttributeList(),
			(useCachedResult, encoder) =>
				Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

		helper.Process(context, output);

		Assert.True(output.Attributes.TryGetAttribute("nonce", out var attr));
		Assert.Equal("abc123", attr.Value);
	}

	private sealed class FixedNonceService(string nonce) : ICspNonceService
	{
		public string GetNonce() => nonce;
	}
}