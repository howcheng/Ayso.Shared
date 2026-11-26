using System.Net;
using Ayso.Shared.Domain.Documents;
using Ayso.Shared.Domain.Interfaces;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IStorageWriteLock"/> using
/// <c>IfGenerationMatch = 0</c> atomic object creation under <c>{competitionSlug}/locks/{lockName}.json</c>.
/// </summary>
public class GcsStorageWriteLock : IStorageWriteLock
{
	private const int MaxAcquireAttempts = 3;

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;
	private readonly string _competitionSlug;

	public GcsStorageWriteLock(StorageClient storageClient, IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		ArgumentNullException.ThrowIfNull(options);

		var opts = options.Value;
		ArgumentException.ThrowIfNullOrWhiteSpace(opts.CompetitionSlug);

		_storageClient = storageClient;
		_bucketName = opts.BucketName;
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
		return IsLockedInternalAsync(lockName, cancellationToken);
	}

	private bool IsLockedInternal(string lockName)
	{
		var existing = TryReadLockDocument(lockName);
		if (existing is null)
		{
			return false;
		}

		return !StorageWriteLockHelper.IsExpired(existing.Value.Document, DateTime.UtcNow);
	}

	private async Task<bool> IsLockedInternalAsync(string lockName, CancellationToken cancellationToken)
	{
		var existing = await TryReadLockDocumentAsync(lockName, cancellationToken);
		if (existing is null)
		{
			return false;
		}

		return !StorageWriteLockHelper.IsExpired(existing.Value.Document, DateTime.UtcNow);
	}

	private (StorageWriteLockDocument Document, long? Generation)? TryReadLockDocument(string lockName)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);

		try
		{
			var obj = _storageClient.GetObject(_bucketName, objectPath);
			using var stream = new MemoryStream();
			_storageClient.DownloadObject(_bucketName, objectPath, stream);
			stream.Position = 0;

			var document = StorageJsonSerializer.Deserialize<StorageWriteLockDocument>(stream);
			return (document, obj.Generation);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
	}

	private async Task<(StorageWriteLockDocument Document, long? Generation)?> TryReadLockDocumentAsync(
		string lockName,
		CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);

		try
		{
			var obj = await _storageClient.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
			await using var stream = new MemoryStream();
			await _storageClient.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
			stream.Position = 0;

			var document = await StorageJsonSerializer.DeserializeAsync<StorageWriteLockDocument>(stream, cancellationToken);
			return (document, obj.Generation);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}
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

		return TryAcquireInternalAsync(lockName, holder, ttl, cancellationToken);
	}

	private IStorageWriteLockHandle? TryAcquireInternal(string lockName, string holder, TimeSpan ttl)
	{
		for (var attempt = 0; attempt < MaxAcquireAttempts; attempt++)
		{
			var utcNow = DateTime.UtcNow;
			var existing = TryReadLockDocument(lockName);
			if (existing is not null)
			{
				if (!StorageWriteLockHelper.IsExpired(existing.Value.Document, utcNow))
				{
					return null;
				}

				DeleteLock(lockName, existing.Value.Generation);
			}

			var created = TryCreateLock(lockName, holder, ttl, utcNow);
			if (created.Acquired)
			{
				return new StorageWriteLockHandle(
					lockName,
					() => ReleaseLockBestEffort(lockName, created.Generation),
					() => ReleaseLockBestEffortAsync(lockName, created.Generation));
			}
		}

		return null;
	}

	private async Task<IStorageWriteLockHandle?> TryAcquireInternalAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < MaxAcquireAttempts; attempt++)
		{
			var utcNow = DateTime.UtcNow;
			var existing = await TryReadLockDocumentAsync(lockName, cancellationToken);
			if (existing is not null)
			{
				if (!StorageWriteLockHelper.IsExpired(existing.Value.Document, utcNow))
				{
					return null;
				}

				await DeleteLockAsync(lockName, existing.Value.Generation, cancellationToken);
			}

			var created = await TryCreateLockAsync(lockName, holder, ttl, utcNow, cancellationToken);
			if (created.Acquired)
			{
				return new StorageWriteLockHandle(
					lockName,
					() => ReleaseLockBestEffort(lockName, created.Generation),
					() => ReleaseLockBestEffortAsync(lockName, created.Generation));
			}
		}

		return null;
	}

	private (bool Acquired, long? Generation) TryCreateLock(
		string lockName,
		string holder,
		TimeSpan ttl,
		DateTime utcNow)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);
		var document = StorageWriteLockHelper.CreateDocument(holder, ttl, utcNow);

		try
		{
			using var stream = new MemoryStream(StorageJsonSerializer.SerializeToUtf8Bytes(document), writable: false);
			var uploadOptions = new UploadObjectOptions
			{
				IfGenerationMatch = 0,
			};

			var uploaded = _storageClient.UploadObject(
				_bucketName,
				objectPath,
				"application/json",
				stream,
				uploadOptions);

			return (true, uploaded?.Generation);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
			return (false, null);
		}
	}

	private async Task<(bool Acquired, long? Generation)> TryCreateLockAsync(
		string lockName,
		string holder,
		TimeSpan ttl,
		DateTime utcNow,
		CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);
		var document = StorageWriteLockHelper.CreateDocument(holder, ttl, utcNow);

		try
		{
			await using var stream = new MemoryStream(StorageJsonSerializer.SerializeToUtf8Bytes(document), writable: false);
			var uploadOptions = new UploadObjectOptions
			{
				IfGenerationMatch = 0,
			};

			var uploaded = await _storageClient.UploadObjectAsync(
				_bucketName,
				objectPath,
				"application/json",
				stream,
				uploadOptions,
				cancellationToken);

			return (true, uploaded?.Generation);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
			return (false, null);
		}
	}

	private void DeleteLock(string lockName, long? generation)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);

		try
		{
			var deleteOptions = generation.HasValue
				? new DeleteObjectOptions { IfGenerationMatch = generation.Value }
				: null;

			_storageClient.DeleteObject(_bucketName, objectPath, deleteOptions);
		}
		catch (GoogleApiException ex) when (
			ex.HttpStatusCode == HttpStatusCode.NotFound ||
			ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
		}
	}

	private async Task DeleteLockAsync(string lockName, long? generation, CancellationToken cancellationToken)
	{
		var objectPath = StorageWriteLockHelper.GetLockObjectPath(lockName, _competitionSlug);

		try
		{
			var deleteOptions = generation.HasValue
				? new DeleteObjectOptions { IfGenerationMatch = generation.Value }
				: null;

			await _storageClient.DeleteObjectAsync(
				_bucketName,
				objectPath,
				deleteOptions,
				cancellationToken);
		}
		catch (GoogleApiException ex) when (
			ex.HttpStatusCode == HttpStatusCode.NotFound ||
			ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
		}
	}

	private void ReleaseLockBestEffort(string lockName, long? createdGeneration)
	{
		try
		{
			var generationToDelete = createdGeneration;
			if (!generationToDelete.HasValue)
			{
				var existing = TryReadLockDocument(lockName);
				if (existing is null)
				{
					return;
				}

				generationToDelete = existing.Value.Generation;
			}

			DeleteLock(lockName, generationToDelete);
		}
		catch (GoogleApiException)
		{
		}
	}

	private async Task ReleaseLockBestEffortAsync(string lockName, long? createdGeneration)
	{
		try
		{
			var generationToDelete = createdGeneration;
			if (!generationToDelete.HasValue)
			{
				var existing = await TryReadLockDocumentAsync(lockName, CancellationToken.None);
				if (existing is null)
				{
					return;
				}

				generationToDelete = existing.Value.Generation;
			}

			await DeleteLockAsync(lockName, generationToDelete, CancellationToken.None);
		}
		catch (GoogleApiException)
		{
		}
	}
}
