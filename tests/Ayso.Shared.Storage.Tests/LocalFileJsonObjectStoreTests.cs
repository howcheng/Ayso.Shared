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
	public async Task SaveAndGet_Async_RoundTripsDocument()
	{
		await _store.SaveAsync("demo/doc.json", new SampleDoc { Name = "alpha" }, expectedGeneration: 0);

		Assert.True(await _store.ExistsAsync("demo/doc.json"));

		var loaded = await _store.GetAsync<SampleDoc>("demo/doc.json");
		Assert.Equal("alpha", loaded.Content.Name);
		Assert.Equal(1L, loaded.Generation);
	}

	[Fact]
	public void SaveAndGet_Sync_RoundTripsAndDeletesDocument()
	{
		_store.Save("demo/sync.json", new SampleDoc { Name = "beta" }, expectedGeneration: 0);

		Assert.True(_store.Exists("demo/sync.json"));

		var loaded = _store.Get<SampleDoc>("demo/sync.json");
		Assert.Equal("beta", loaded.Content.Name);
		Assert.Equal(1L, loaded.Generation);

		_store.Delete("demo/sync.json", expectedGeneration: 1L);
		Assert.False(_store.Exists("demo/sync.json"));
	}

	[Fact]
	public async Task Save_WithWrongGeneration_ThrowsConcurrencyException()
	{
		await _store.SaveAsync("demo/doc.json", new SampleDoc { Name = "a" }, expectedGeneration: 0);

		Assert.Throws<StorageConcurrencyException>(() =>
			_store.Save("demo/doc.json", new SampleDoc { Name = "b" }, expectedGeneration: 99));

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
