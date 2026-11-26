using Ayso.Shared.Storage;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage.Tests;

public class GcsStorageWriteLockTests
{
	[Fact]
	public void TryAcquire_Sync_AcquiresBlocksAndReleasesViaDispose()
	{
		var writeLock = CreateWriteLock();

		using (var handle = writeLock.TryAcquire("users", "admin-a", TimeSpan.FromMinutes(2)))
		{
			Assert.NotNull(handle);
			Assert.True(writeLock.IsLocked("users"));
			Assert.Null(writeLock.TryAcquire("users", "admin-b", TimeSpan.FromMinutes(2)));
		}

		Assert.False(writeLock.IsLocked("users"));
	}

	[Fact]
	public async Task TryAcquireAsync_AcquiresBlocksAndReleasesViaDisposeAsync()
	{
		var writeLock = CreateWriteLock();

		await using (var handle = await writeLock.TryAcquireAsync("csv-import", "admin-a", TimeSpan.FromMinutes(2)))
		{
			Assert.NotNull(handle);
			Assert.True(await writeLock.IsLockedAsync("csv-import"));
			Assert.Null(await writeLock.TryAcquireAsync("csv-import", "admin-b", TimeSpan.FromMinutes(2)));
		}

		Assert.False(await writeLock.IsLockedAsync("csv-import"));
	}

	private static GcsStorageWriteLock CreateWriteLock()
	{
		return new GcsStorageWriteLock(
			new GcsJsonObjectStoreTests.FakeJsonStorageClient(),
			Options.Create(new StorageOptions
			{
				Provider = "Gcs",
				BucketName = "area10e-lock-bucket",
				CompetitionSlug = "area10e-14u",
			}));
	}
}
