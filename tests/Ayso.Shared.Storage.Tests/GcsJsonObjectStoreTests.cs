using System.Net;
using Ayso.Shared.Storage;
using Google;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Ayso.Shared.Storage.Tests;

public class GcsJsonObjectStoreTests
{
	private const string TestBucket = "area10e-json-bucket";

	[Fact]
	public async Task SaveGetExistsDelete_Async_RoundTripsWithGenerationConcurrency()
	{
		var store = CreateStore(new FakeJsonStorageClient());
		const string path = "area10e-14u/index.json";

		Assert.False(await store.ExistsAsync(path));

		await store.SaveAsync(path, new SamplePayload { Title = "v1" }, expectedGeneration: 0);
		Assert.True(await store.ExistsAsync(path));

		var loaded = await store.GetAsync<SamplePayload>(path);
		Assert.Equal("v1", loaded.Content.Title);
		Assert.Equal(1L, loaded.Generation);

		await Assert.ThrowsAsync<StorageConcurrencyException>(() =>
			store.SaveAsync(path, new SamplePayload { Title = "stale" }, expectedGeneration: 99));

		await store.SaveAsync(path, new SamplePayload { Title = "v2" }, expectedGeneration: 1L);
		var updated = await store.GetAsync<SamplePayload>(path);
		Assert.Equal("v2", updated.Content.Title);
		Assert.Equal(2L, updated.Generation);

		await store.DeleteAsync(path, expectedGeneration: 2L);
		Assert.False(await store.ExistsAsync(path));
	}

	[Fact]
	public void SaveGetExistsDelete_Sync_RoundTripsWithGenerationConcurrency()
	{
		var store = CreateStore(new FakeJsonStorageClient());
		const string path = "area10e-14u/locations.json";

		Assert.False(store.Exists(path));

		store.Save(path, new SamplePayload { Title = "sync-v1" }, expectedGeneration: 0);
		Assert.True(store.Exists(path));

		var loaded = store.Get<SamplePayload>(path);
		Assert.Equal("sync-v1", loaded.Content.Title);
		Assert.Equal(1L, loaded.Generation);

		Assert.Throws<StorageConcurrencyException>(() =>
			store.Save(path, new SamplePayload { Title = "stale" }, expectedGeneration: 99));

		store.Delete(path, expectedGeneration: 1L);
		Assert.False(store.Exists(path));
	}

	private static GcsJsonObjectStore CreateStore(StorageClient client)
	{
		return new GcsJsonObjectStore(
			client,
			Options.Create(new StorageOptions { Provider = "Gcs", BucketName = TestBucket }));
	}

	private sealed class SamplePayload
	{
		public string Title { get; set; } = string.Empty;
	}

	internal sealed class FakeJsonStorageClient : StorageClient
	{
		private readonly Dictionary<(string Bucket, string Name), (byte[] Bytes, long Generation)> _objects = new();

		public override GcsObject UploadObject(
			string bucket,
			string objectName,
			string? contentType,
			Stream source,
			UploadObjectOptions? options = null,
			IProgress<IUploadProgress>? progress = null)
		{
			var exists = _objects.TryGetValue((bucket, objectName), out var current);
			if (options?.IfGenerationMatch is long expected)
			{
				var matches = expected == 0 ? !exists : (exists && current.Generation == expected);
				if (!matches)
				{
					throw CreateException(HttpStatusCode.PreconditionFailed);
				}
			}

			using var copy = new MemoryStream();
			source.CopyTo(copy);

			var nextGen = (exists ? current.Generation : 0L) + 1L;
			_objects[(bucket, objectName)] = (copy.ToArray(), nextGen);
			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = contentType, Generation = nextGen };
		}

		public override Task<GcsObject> UploadObjectAsync(
			string bucket,
			string objectName,
			string? contentType,
			Stream source,
			UploadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<IUploadProgress>? progress = null)
		{
			return Task.FromResult(UploadObject(bucket, objectName, contentType, source, options, progress));
		}

		public override GcsObject DownloadObject(
			string bucket,
			string objectName,
			Stream destination,
			DownloadObjectOptions? options = null,
			IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
		{
			if (!_objects.TryGetValue((bucket, objectName), out var entry))
			{
				throw CreateException(HttpStatusCode.NotFound);
			}

			destination.Write(entry.Bytes, 0, entry.Bytes.Length);
			return new GcsObject { Bucket = bucket, Name = objectName, Generation = entry.Generation };
		}

		public override Task<GcsObject> DownloadObjectAsync(
			string bucket,
			string objectName,
			Stream destination,
			DownloadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
		{
			return Task.FromResult(DownloadObject(bucket, objectName, destination, options, progress));
		}

		public override GcsObject GetObject(string bucket, string objectName, GetObjectOptions? options = null)
		{
			if (!_objects.TryGetValue((bucket, objectName), out var entry))
			{
				throw CreateException(HttpStatusCode.NotFound);
			}

			return new GcsObject { Bucket = bucket, Name = objectName, Generation = entry.Generation };
		}

		public override Task<GcsObject> GetObjectAsync(
			string bucket,
			string objectName,
			GetObjectOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(GetObject(bucket, objectName, options));
		}

		public override void DeleteObject(string bucket, string objectName, DeleteObjectOptions? options = null)
		{
			if (!_objects.TryGetValue((bucket, objectName), out var current))
			{
				throw CreateException(HttpStatusCode.NotFound);
			}

			if (options?.IfGenerationMatch is long expected && current.Generation != expected)
			{
				throw CreateException(HttpStatusCode.PreconditionFailed);
			}

			_objects.Remove((bucket, objectName));
		}

		public override Task DeleteObjectAsync(
			string bucket,
			string objectName,
			DeleteObjectOptions? options = null,
			CancellationToken cancellationToken = default)
		{
			DeleteObject(bucket, objectName, options);
			return Task.CompletedTask;
		}

		private static GoogleApiException CreateException(HttpStatusCode statusCode)
		{
			return new GoogleApiException("storage", statusCode.ToString())
			{
				HttpStatusCode = statusCode,
			};
		}
	}
}
