using System.Net;
using Ayso.Shared.Domain.Interfaces;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IBlobObjectStore"/> with transient rate-limit retries.
/// </summary>
public class GcsBlobObjectStore : IBlobObjectStore
{
	private const int MaxRetryAttempts = 3;

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;

	public GcsBlobObjectStore(StorageClient storageClient, IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		ArgumentNullException.ThrowIfNull(options);

		_storageClient = storageClient;
		_bucketName = options.Value.BucketName;
	}

	public void Upload(string objectPath, Stream content, string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		UploadInternal(objectPath, content, contentType);
	}

	public Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		return UploadInternalAsync(objectPath, content, contentType, cancellationToken);
	}

	private void UploadInternal(string objectPath, Stream content, string contentType)
	{
		var canReplayStream = content.CanSeek;
		var initialPosition = canReplayStream ? content.Position : 0L;

		for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
		{
			try
			{
				if (attempt > 0 && canReplayStream)
				{
					content.Position = initialPosition;
				}

				_storageClient.UploadObject(_bucketName, objectPath, contentType, content);
				return;
			}
			catch (GoogleApiException ex) when (IsTransientUploadFailure(ex, attempt, canReplayStream))
			{
				Thread.Sleep(GetRetryDelay(attempt));
			}
		}
	}

	private async Task UploadInternalAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken)
	{
		var canReplayStream = content.CanSeek;
		var initialPosition = canReplayStream ? content.Position : 0L;

		for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
		{
			try
			{
				if (attempt > 0 && canReplayStream)
				{
					content.Position = initialPosition;
				}

				await _storageClient.UploadObjectAsync(
					_bucketName,
					objectPath,
					contentType,
					content,
					cancellationToken: cancellationToken);

				return;
			}
			catch (GoogleApiException ex) when (IsTransientUploadFailure(ex, attempt, canReplayStream))
			{
				await Task.Delay(GetRetryDelay(attempt), cancellationToken);
			}
		}
	}

	private static bool IsTransientUploadFailure(GoogleApiException exception, int attempt, bool canReplayStream)
	{
		if (!canReplayStream || attempt >= MaxRetryAttempts - 1)
		{
			return false;
		}

		return exception.HttpStatusCode == HttpStatusCode.TooManyRequests ||
			exception.HttpStatusCode == HttpStatusCode.ServiceUnavailable;
	}

	private static TimeSpan GetRetryDelay(int attempt)
	{
		return TimeSpan.FromMilliseconds(200 * (attempt + 1));
	}

	public Stream OpenRead(string objectPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return OpenReadInternal(objectPath);
	}

	public Task<Stream> OpenReadAsync(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return OpenReadInternalAsync(objectPath, cancellationToken);
	}

	private Stream OpenReadInternal(string objectPath)
	{
		var stream = new MemoryStream();
		try
		{
			_storageClient.DownloadObject(_bucketName, objectPath, stream);
			stream.Position = 0;
			return stream;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			stream.Dispose();
			throw new FileNotFoundException($"Storage object not found: {objectPath}", ex);
		}
		catch
		{
			stream.Dispose();
			throw;
		}
	}

	private async Task<Stream> OpenReadInternalAsync(string objectPath, CancellationToken cancellationToken)
	{
		var stream = new MemoryStream();
		try
		{
			await _storageClient.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
			stream.Position = 0;
			return stream;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			await stream.DisposeAsync();
			throw new FileNotFoundException($"Storage object not found: {objectPath}", ex);
		}
		catch
		{
			await stream.DisposeAsync();
			throw;
		}
	}

	public bool Exists(string objectPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return ExistsInternal(objectPath);
	}

	public Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return ExistsInternalAsync(objectPath, cancellationToken);
	}

	private bool ExistsInternal(string objectPath)
	{
		try
		{
			_storageClient.GetObject(_bucketName, objectPath);
			return true;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	private async Task<bool> ExistsInternalAsync(string objectPath, CancellationToken cancellationToken)
	{
		try
		{
			await _storageClient.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
			return true;
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}
	}

	public void Delete(string objectPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		DeleteInternal(objectPath);
	}

	public Task DeleteAsync(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return DeleteInternalAsync(objectPath, cancellationToken);
	}

	private void DeleteInternal(string objectPath)
	{
		try
		{
			_storageClient.DeleteObject(_bucketName, objectPath);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
		}
	}

	private async Task DeleteInternalAsync(string objectPath, CancellationToken cancellationToken)
	{
		try
		{
			await _storageClient.DeleteObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
		}
	}
}
