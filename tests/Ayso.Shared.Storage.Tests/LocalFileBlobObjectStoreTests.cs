using Ayso.Shared.Storage;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage.Tests;

public class LocalFileBlobObjectStoreTests : IDisposable
{
	private readonly string _tempRoot;
	private readonly LocalFileBlobObjectStore _store;

	public LocalFileBlobObjectStoreTests()
	{
		_tempRoot = Path.Combine(Path.GetTempPath(), "ayso-shared-blobs-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempRoot);
		_store = new LocalFileBlobObjectStore(Options.Create(new StorageOptions { LocalRoot = _tempRoot }));
	}

	[Fact]
	public async Task UploadAndOpenReadAsync_RoundTripsBinaryPayload()
	{
		var expected = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
		const string path = "2026/area10e-14u/scorecards/14u-boys/42/card-1.jpg";

		await using (var uploadStream = new MemoryStream(expected))
		{
			await _store.UploadAsync(path, uploadStream, "image/jpeg");
		}

		Assert.True(await _store.ExistsAsync(path));

		await using var readStream = await _store.OpenReadAsync(path);
		using var copy = new MemoryStream();
		await readStream.CopyToAsync(copy);

		Assert.Equal(expected, copy.ToArray());
	}

	[Fact]
	public void UploadAndOpenRead_Sync_RoundTripsAndOverwritesAtomically()
	{
		var firstPayload = new byte[] { 1, 2, 3, 4 };
		var secondPayload = new byte[] { 9, 8, 7, 6, 5 };
		const string path = "2026/area10e-14u/scorecards/14u-girls/12/card.png";

		using (var firstStream = new MemoryStream(firstPayload))
		{
			_store.Upload(path, firstStream, "image/png");
		}

		using (var secondStream = new MemoryStream(secondPayload))
		{
			_store.Upload(path, secondStream, "image/png");
		}

		Assert.True(_store.Exists(path));

		using var readStream = _store.OpenRead(path);
		using var copy = new MemoryStream();
		readStream.CopyTo(copy);

		Assert.Equal(secondPayload, copy.ToArray());
	}

	[Fact]
	public async Task Delete_SyncAndAsync_RemovesBlobAndIsIdempotent()
	{
		const string asyncPath = "blobs/a.webp";
		const string syncPath = "blobs/b.webp";

		await _store.UploadAsync(asyncPath, new MemoryStream([1, 2, 3]), "image/webp");
		_store.Upload(syncPath, new MemoryStream([4, 5, 6]), "image/webp");

		await _store.DeleteAsync(asyncPath);
		await _store.DeleteAsync(asyncPath);

		_store.Delete(syncPath);
		_store.Delete(syncPath);

		Assert.False(await _store.ExistsAsync(asyncPath));
		Assert.False(_store.Exists(syncPath));
	}

	[Fact]
	public async Task OpenRead_WhenMissing_ThrowsFileNotFoundException()
	{
		Assert.Throws<FileNotFoundException>(() => _store.OpenRead("missing/photo.jpg"));
		await Assert.ThrowsAsync<FileNotFoundException>(() => _store.OpenReadAsync("missing/photo.jpg"));
	}

	[Theory]
	[InlineData("../outside.jpg")]
	[InlineData("nested/../../outside.jpg")]
	[InlineData("/leading-slash.jpg")]
	[InlineData("\\leading-backslash.jpg")]
	public async Task PathTraversalOrRootedPath_ThrowsArgumentException(string invalidPath)
	{
		Assert.Throws<ArgumentException>(() => _store.Upload(invalidPath, new MemoryStream([1]), "image/jpeg"));
		await Assert.ThrowsAsync<ArgumentException>(() => _store.UploadAsync(invalidPath, new MemoryStream([1]), "image/jpeg"));
		Assert.Throws<ArgumentException>(() => _store.Exists(invalidPath));
		Assert.Throws<ArgumentException>(() => _store.OpenRead(invalidPath));
		Assert.Throws<ArgumentException>(() => _store.Delete(invalidPath));
	}

	[Fact]
	public async Task Validation_NullOrWhitespaceArguments_ThrowSynchronously()
	{
		using var stream = new MemoryStream([1, 2, 3]);

		Assert.ThrowsAny<ArgumentException>(() => _store.Upload(" ", stream, "image/jpeg"));
		Assert.Throws<ArgumentNullException>(() => _store.Upload("valid/path.jpg", null!, "image/jpeg"));
		Assert.ThrowsAny<ArgumentException>(() => _store.Upload("valid/path.jpg", stream, " "));

		Assert.ThrowsAny<ArgumentException>(() => { _ = _store.UploadAsync(" ", stream, "image/jpeg"); });
		Assert.Throws<ArgumentNullException>(() => { _ = _store.UploadAsync("valid/path.jpg", null!, "image/jpeg"); });
		Assert.ThrowsAny<ArgumentException>(() => { _ = _store.UploadAsync("valid/path.jpg", stream, " "); });
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
