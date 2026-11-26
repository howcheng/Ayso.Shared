using Ayso.Shared.Storage;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage.Tests;

public class LocalFileJsonObjectStoreTests : IDisposable
{
	private readonly string _tempRoot;
	private readonly LocalFileJsonObjectStore _store;

	public LocalFileJsonObjectStoreTests()
	{
		_tempRoot = Path.Combine(Path.GetTempPath(), "ayso-shared-store-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempRoot);
		_store = new LocalFileJsonObjectStore(Options.Create(new StorageOptions { LocalRoot = _tempRoot }));
	}

	[Fact]
	public async Task SaveAndGet_RoundTripsDocument()
	{
		await _store.SaveAsync("demo/doc.json", new SampleDoc { Name = "alpha" }, expectedGeneration: 0);

		var loaded = await _store.GetAsync<SampleDoc>("demo/doc.json");
		Assert.Equal("alpha", loaded.Content.Name);
		Assert.Equal(1L, loaded.Generation);
	}

	[Fact]
	public async Task Save_WithWrongGeneration_ThrowsConcurrencyException()
	{
		await _store.SaveAsync("demo/doc.json", new SampleDoc { Name = "a" }, expectedGeneration: 0);

		await Assert.ThrowsAsync<StorageConcurrencyException>(() =>
			_store.SaveAsync("demo/doc.json", new SampleDoc { Name = "b" }, expectedGeneration: 99));
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}

	private sealed class SampleDoc
	{
		public string Name { get; set; } = string.Empty;
	}
}