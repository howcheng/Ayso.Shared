using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Ayso.Shared.AspNet.Services;

/// <summary>
/// Generates and caches a 256-bit Base64-encoded cryptographic nonce scoped to a single HTTP request.
/// </summary>
public class CspNonceService : ICspNonceService
{
	public const int NonceByteLength = 32;
	public const string HttpContextItemKey = "Ayso.Shared.CspNonce";

	private readonly IHttpContextAccessor? _httpContextAccessor;
	private string? _nonce;

	public CspNonceService(IHttpContextAccessor? httpContextAccessor = null)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	public string GetNonce()
	{
		var httpContext = _httpContextAccessor?.HttpContext;
		if (httpContext is not null &&
			httpContext.Items.TryGetValue(HttpContextItemKey, out var existing) &&
			existing is string cachedNonce &&
			!string.IsNullOrEmpty(cachedNonce))
		{
			_nonce = cachedNonce;
			return cachedNonce;
		}

		if (string.IsNullOrEmpty(_nonce))
		{
			_nonce = GenerateNonce();
		}

		if (httpContext is not null)
		{
			httpContext.Items[HttpContextItemKey] = _nonce;
		}

		return _nonce;
	}

	private static string GenerateNonce()
	{
		Span<byte> bytes = stackalloc byte[NonceByteLength];
		RandomNumberGenerator.Fill(bytes);
		return Convert.ToBase64String(bytes);
	}
}