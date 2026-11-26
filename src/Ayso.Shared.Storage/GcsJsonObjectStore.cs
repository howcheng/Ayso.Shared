using System.Net;
using Ayso.Shared.Domain.Documents;
using Ayso.Shared.Domain.Interfaces;
using Google;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IJsonObjectStore"/> with
/// <c>IfGenerationMatch</c> optimistic concurrency and transient rate-limit retries.
/// </summary>
public class GcsJsonObjectStore : IJsonObjectStore
{
	private const int MaxRetryAttempts = 3;

	private readonly StorageClient _storageClient;
	private readonly string _bucketName;

	public GcsJsonObjectStore(StorageClient storageClient, IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(storageClient);
		ArgumentNullException.ThrowIfNull(options);

		_storageClient = storageClient;
		_bucketName = options.Value.BucketName;
	}

	public Task<StorageDocument<T>> GetAsync<T>(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return GetObjectAsync<T>(objectPath, cancellationToken);
	}

	public Task SaveAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(document);
		return SaveObjectAsync(objectPath, document, expectedGeneration, cancellationToken);
	}

	public Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return ExistsInternalAsync(objectPath, cancellationToken);
	}

	public Task DeleteAsync(string objectPath, long? expectedGeneration = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return DeleteInternalAsync(objectPath, expectedGeneration, cancellationToken);
	}

	private async Task<StorageDocument<T>> GetObjectAsync<T>(string objectPath, CancellationToken cancellationToken)
	{
		try
		{
			var obj = await _storageClient.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
			await using var stream = new MemoryStream();
			await _storageClient.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
			stream.Position = 0;

			return new StorageDocument<T>
			{
				Content = await StorageJsonSerializer.DeserializeAsync<T>(stream, cancellationToken),
				Generation = obj.Generation,
				ObjectPath = objectPath,
			};
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
			throw new FileNotFoundException($"Storage object not found: {objectPath}", ex);
		}
	}

	private async Task SaveObjectAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken)
	{
		var payload = StorageJsonSerializer.SerializeToUtf8Bytes(document);

		for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
		{
			try
			{
				await using var stream = new MemoryStream(payload, writable: false);
				var uploadOptions = new UploadObjectOptions
				{
					IfGenerationMatch = expectedGeneration,
				};

				await _storageClient.UploadObjectAsync(
					_bucketName,
					objectPath,
					"application/json",
					stream,
					uploadOptions,
					cancellationToken);

				return;
			}
			catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
			{
				if (expectedGeneration.HasValue || attempt == MaxRetryAttempts - 1)
				{
					throw new StorageConcurrencyException(objectPath, ex);
				}

				await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken);
			}
			catch (GoogleApiException ex) when (
				(ex.HttpStatusCode == HttpStatusCode.TooManyRequests || ex.HttpStatusCode == HttpStatusCode.ServiceUnavailable) &&
				attempt < MaxRetryAttempts - 1)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), cancellationToken);
			}
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

	private async Task DeleteInternalAsync(string objectPath, long? expectedGeneration, CancellationToken cancellationToken)
	{
		try
		{
			var deleteOptions = expectedGeneration.HasValue
				? new DeleteObjectOptions { IfGenerationMatch = expectedGeneration.Value }
				: null;

			await _storageClient.DeleteObjectAsync(_bucketName, objectPath, deleteOptions, cancellationToken);
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
		{
		}
		catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
		{
			throw new StorageConcurrencyException(objectPath, ex);
		}
	}
}