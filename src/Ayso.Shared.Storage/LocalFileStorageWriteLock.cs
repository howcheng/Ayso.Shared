using Ayso.Shared.Domain.Documents;
using Ayso.Shared.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IStorageWriteLock"/> stored under
/// <c>{competitionSlug}/locks/{lockName}.json</c>.
/// </summary>
public class LocalFileStorageWriteLock : IStorageWriteLock
{
	private readonly string _localRoot;
	private readonly string _competitionSlug;
	private readonly object _syncRoot = new();

	public LocalFileStorageWriteLock(IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var opts = options.Value;
		ArgumentException.ThrowIfNullOrWhiteSpace(opts.CompetitionSlug);

		_localRoot = Path.GetFullPath(opts.LocalRoot);
		_competitionSlug = opts.CompetitionSlug;
	}

	public bool IsLocked(string lockName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockName);
		return IsLockedInternal(lockName);
	}

	public Task<bool> IsLockedAsync(string lockName, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockName);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(IsLockedInternal(lockName));
	}

	private bool IsLockedInternal(string lockName)
	{
		lock (_syncRoot)
		{
			var document = ReadLockDocument(lockName);
			if (document is null)
			{
				return false;
			}

			return !StorageWriteLockHelper.IsExpired(document, DateTime.UtcNow);
		}
	}

	private StorageWriteLockDocument? ReadLockDocument(string lockName)
	{
		var path = GetLockFilePath(lockName);
		if (!File.Exists(path))
		{
			return null;
		}

		var json = File.ReadAllText(path, StorageJsonSerializer.Utf8WithoutBom);
		return StorageJsonSerializer.Deserialize<StorageWriteLockDocument>(json);
	}

	private string GetLockFilePath(string lockName)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);
		return Path.Combine(_localRoot, objectPath.Replace('/', Path.DirectorySeparatorChar));
	}

	public IStorageWriteLockHandle? TryAcquire(string lockName, string holder, TimeSpan ttl)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockName);
		ArgumentException.ThrowIfNullOrWhiteSpace(holder);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

		return TryAcquireInternal(lockName, holder, ttl);
	}

	public Task<IStorageWriteLockHandle?> TryAcquireAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockName);
		ArgumentException.ThrowIfNullOrWhiteSpace(holder);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
		cancellationToken.ThrowIfCancellationRequested();

		return Task.FromResult(TryAcquireInternal(lockName, holder, ttl));
	}

	private IStorageWriteLockHandle? TryAcquireInternal(string lockName, string holder, TimeSpan ttl)
	{
		lock (_syncRoot)
		{
			var utcNow = DateTime.UtcNow;
			var existing = ReadLockDocument(lockName);
			if (existing is not null)
			{
				if (!StorageWriteLockHelper.IsExpired(existing, utcNow))
				{
					return null;
				}

				DeleteLockFile(lockName);
			}

			var lockDocument = StorageWriteLockHelper.CreateDocument(holder, ttl, utcNow);
			WriteLockDocument(lockName, lockDocument);

			return new StorageWriteLockHandle(
				lockName,
				() => ReleaseLockBestEffort(lockName, lockDocument.Holder, lockDocument.AcquiredAtUtc),
				() =>
				{
					ReleaseLockBestEffort(lockName, lockDocument.Holder, lockDocument.AcquiredAtUtc);
					return Task.CompletedTask;
				});
		}
	}

	private void WriteLockDocument(string lockName, StorageWriteLockDocument document)
	{
		var path = GetLockFilePath(lockName);
		var directory = Path.GetDirectoryName(path)!;
		Directory.CreateDirectory(directory);

		var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
		File.WriteAllText(tempPath, StorageJsonSerializer.Serialize(document), StorageJsonSerializer.Utf8WithoutBom);
		File.Move(tempPath, path, overwrite: true);
	}

	private void DeleteLockFile(string lockName)
	{
		var path = GetLockFilePath(lockName);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private void ReleaseLockBestEffort(string lockName, string holder, DateTime acquiredAtUtc)
	{
		lock (_syncRoot)
		{
			var existing = ReadLockDocument(lockName);
			if (existing is not null &&
				string.Equals(existing.Holder, holder, StringComparison.Ordinal) &&
				existing.AcquiredAtUtc == acquiredAtUtc)
			{
				DeleteLockFile(lockName);
			}
		}
	}
}
