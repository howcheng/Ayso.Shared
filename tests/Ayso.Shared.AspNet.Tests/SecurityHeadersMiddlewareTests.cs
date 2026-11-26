using Ayso.Shared.AspNet.Middleware;
using Ayso.Shared.AspNet.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ayso.Shared.AspNet.Tests;

public class SecurityHeadersMiddlewareTests
{
	[Fact]
	public async Task InvokeAsync_DefaultOptionsInProduction_PreservesBaselineHeaders()
	{
		var middleware = new SecurityHeadersMiddleware(
			_ => Task.CompletedTask,
			new FakeWebHostEnvironment(Environments.Production),
			NullLogger<SecurityHeadersMiddleware>.Instance);

		var context = new DefaultHttpContext();
		await middleware.InvokeAsync(context, new FixedNonceService("nonce123"));

		var csp = context.Response.Headers["Content-Security-Policy"].ToString();
		Assert.Equal(
			"default-src 'self'; " +
			"script-src 'nonce-nonce123' 'strict-dynamic'; " +
			"object-src 'none'; " +
			"style-src 'self'; " +
			"img-src 'self' data:; " +
			"font-src 'self'; " +
			"connect-src 'self'; " +
			"frame-ancestors 'none'; " +
			"base-uri 'self'; " +
			"form-action 'self'",
			csp);
		Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
		Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
		Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
		Assert.Equal("camera=(), microphone=(), geolocation=()", context.Response.Headers["Permissions-Policy"].ToString());
		Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
		Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"].ToString());
	}

	[Fact]
	public async Task InvokeAsync_DefaultOptionsInDevelopment_IncludesLocalhostConnectSources()
	{
		var middleware = new SecurityHeadersMiddleware(
			_ => Task.CompletedTask,
			new FakeWebHostEnvironment(Environments.Development),
			NullLogger<SecurityHeadersMiddleware>.Instance);

		var context = new DefaultHttpContext();
		await middleware.InvokeAsync(context, new FixedNonceService("devnonce"));

		var csp = context.Response.Headers["Content-Security-Policy"].ToString();
		Assert.Contains("connect-src 'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*;", csp);
		Assert.Equal("camera=(), microphone=(), geolocation=(), unload=*", context.Response.Headers["Permissions-Policy"].ToString());
	}

	[Fact]
	public async Task UseSecurityHeaders_WithIdentityPlatformAndBlobOptions_AppendsSourcesAndResolvesCoopPerRoute()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment(Environments.Production));
		services.AddScoped<ICspNonceService>(_ => new FixedNonceService("authnonce"));
		await using var provider = services.BuildServiceProvider();

		var appBuilder = new ApplicationBuilder(provider);
		appBuilder.UseSecurityHeaders(options =>
		{
			options.AdditionalConnectSources.Add("https://identitytoolkit.googleapis.com");
			options.AdditionalConnectSources.Add("https://securetoken.googleapis.com");
			options.AdditionalConnectSources.Add("https://identitytoolkit.googleapis.com"); // duplicate ignored
			options.AdditionalFrameSources.Add("https://*.firebaseapp.com");
			options.AdditionalImageSources.Add("blob:");
			options.CrossOriginOpenerPolicyResolver = ctx =>
				ctx.Request.Path.StartsWithSegments("/Account")
					? "same-origin-allow-popups"
					: null;
		});
		appBuilder.Run(_ => Task.CompletedTask);
		var pipeline = appBuilder.Build();

		await using (var scope = provider.CreateAsyncScope())
		{
			var accountContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
			accountContext.Request.Path = "/Account/Login";
			await pipeline(accountContext);

			var csp = accountContext.Response.Headers["Content-Security-Policy"].ToString();
			Assert.Contains("img-src 'self' data: blob:;", csp);
			Assert.Contains("connect-src 'self' https://identitytoolkit.googleapis.com https://securetoken.googleapis.com;", csp);
			Assert.Contains("frame-src 'self' https://*.firebaseapp.com;", csp);
			Assert.Equal("same-origin-allow-popups", accountContext.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
		}

		await using (var scope = provider.CreateAsyncScope())
		{
			var publicContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
			publicContext.Request.Path = "/Scores/Entry";
			await pipeline(publicContext);

			Assert.Equal("same-origin", publicContext.Response.Headers["Cross-Origin-Opener-Policy"].ToString());
		}
	}

	[Fact]
	public void Constructor_WithInvalidDirectiveCharacters_ThrowsArgumentException()
	{
		var options = new SecurityHeadersOptions();
		options.AdditionalConnectSources.Add("https://example.com; script-src 'unsafe-inline'");

		Assert.Throws<ArgumentException>(() => new SecurityHeadersMiddleware(
			_ => Task.CompletedTask,
			new FakeWebHostEnvironment(Environments.Production),
			NullLogger<SecurityHeadersMiddleware>.Instance,
			options));
	}

	private sealed class FixedNonceService(string nonce) : ICspNonceService
	{
		public string GetNonce() => nonce;
	}

	private sealed class FakeWebHostEnvironment(string environmentName) : IWebHostEnvironment
	{
		public string EnvironmentName { get; set; } = environmentName;

		public string ApplicationName { get; set; } = "Ayso.Shared.AspNet.Tests";

		public string WebRootPath { get; set; } = string.Empty;

		public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

		public string ContentRootPath { get; set; } = string.Empty;

		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
