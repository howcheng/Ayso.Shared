using Microsoft.AspNetCore.Http;

namespace Ayso.Shared.AspNet.Middleware;

/// <summary>
/// Options for customizing Content Security Policy sources and Cross-Origin-Opener-Policy
/// in <see cref="SecurityHeadersMiddleware"/> without relaxing baseline defaults.
/// </summary>
public class SecurityHeadersOptions
{
	public const string DefaultCrossOriginOpenerPolicy = "same-origin";

	/// <summary>
	/// Additional origins or schemes appended to the <c>connect-src</c> directive
	/// (e.g., <c>https://identitytoolkit.googleapis.com</c>, <c>https://securetoken.googleapis.com</c>).
	/// </summary>
	public IList<string> AdditionalConnectSources { get; } = new List<string>();

	/// <summary>
	/// Additional origins or schemes appended to <c>frame-src 'self'</c> when non-empty
	/// (e.g., <c>https://*.firebaseapp.com</c> for OAuth auth handlers).
	/// </summary>
	public IList<string> AdditionalFrameSources { get; } = new List<string>();

	/// <summary>
	/// Additional origins or schemes appended to <c>img-src 'self' data:</c>
	/// (e.g., <c>blob:</c> for client-side image previews).
	/// </summary>
	public IList<string> AdditionalImageSources { get; } = new List<string>();

	/// <summary>
	/// Default value for the <c>Cross-Origin-Opener-Policy</c> header. Defaults to <c>same-origin</c>.
	/// </summary>
	public string CrossOriginOpenerPolicy { get; set; } = DefaultCrossOriginOpenerPolicy;

	/// <summary>
	/// Optional per-request resolver for <c>Cross-Origin-Opener-Policy</c> (for example, returning
	/// <c>same-origin-allow-popups</c> on <c>/Account/*</c> routes). Returning <see langword="null"/>
	/// falls back to <see cref="CrossOriginOpenerPolicy"/>.
	/// </summary>
	public Func<HttpContext, string?>? CrossOriginOpenerPolicyResolver { get; set; }
}
