using Ayso.Shared.Storage;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage.Tests;

public class LocalFileStorageWriteLockTests : IDisposable
{
	private readonly string _tempRoot;
	private readonly LocalFileStorageWriteLock _lock;

	public LocalFileStorageWriteLockTests()
	{
		_tempRoot = Path.Combine(Path.GetTempPath(), "ayso-shared-locks-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempRoot);

		var options = Options.Create(new StorageOptions
		{
			LocalRoot = _tempRoot,
			CompetitionSlug = "test-comp",
		});

		_lock = new LocalFileStorageWriteLock(options);
	}

	[Fact]
	public async Task TryAcquireAsync_SucceedsWhenUnlocked()
	{
		await using var handle = await _lock.TryAcquireAsync("import", "tester", TimeSpan.FromMinutes(1));
		Assert.NotNull(handle);
		Assert.True(await _lock.IsLockedAsync("import"));
	}

	[Fact]
	public void TryAcquire_Sync_AcquiresBlocksAndReleasesViaDispose()
	{
		using (var handle = _lock.TryAcquire("sync-import", "tester", TimeSpan.FromMinutes(1)))
		{
			Assert.NotNull(handle);
			Assert.True(_lock.IsLocked("sync-import"));
			Assert.Null(_lock.TryAcquire("sync-import", "other", TimeSpan.FromMinutes(1)));
		}

		Assert.False(_lock.IsLocked("sync-import"));

		using var reacquired = _lock.TryAcquire("sync-import", "other", TimeSpan.FromMinutes(1));
		Assert.NotNull(reacquired);
	}

	[Fact]
	public async Task TryAcquireAsync_FailsWhenHeld()
	{
		await using var first = await _lock.TryAcquireAsync("import", "holder-a", TimeSpan.FromMinutes(5));
		Assert.NotNull(first);

		var second = await _lock.TryAcquireAsync("import", "holder-b", TimeSpan.FromMinutes(5));
		Assert.Null(second);
	}

	[Fact]
	public async Task DisposeAsync_ReleasesLock()
	{
		var handle = await _lock.TryAcquireAsync("import", "holder", TimeSpan.FromMinutes(5));
		Assert.NotNull(handle);
		await handle!.DisposeAsync();

		Assert.False(await _lock.IsLockedAsync("import"));
		await using var again = await _lock.TryAcquireAsync("import", "holder-2", TimeSpan.FromMinutes(1));
		Assert.NotNull(again);
	}

	[Fact]
	public async Task TryAcquireAsync_ReplacesExpiredLock()
	{
		await using var first = await _lock.TryAcquireAsync("import", "old", TimeSpan.FromMilliseconds(1));
		Assert.NotNull(first);
		await Task.Delay(20);

		await using var second = await _lock.TryAcquireAsync("import", "new", TimeSpan.FromMinutes(1));
		Assert.NotNull(second);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
