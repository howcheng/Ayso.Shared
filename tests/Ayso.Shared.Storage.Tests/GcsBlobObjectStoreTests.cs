using System.Net;
using Ayso.Shared.Storage;
using Google;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using GcsObject = Google.Apis.Storage.v1.Data.Object;

namespace Ayso.Shared.Storage.Tests;

public class GcsBlobObjectStoreTests
{
	private const string TestBucket = "area10e-test-bucket";

	[Fact]
	public async Task UploadExistsOpenReadDelete_Async_RoundTripsExpectedContent()
	{
		var fakeClient = new FakeStorageClient();
		var store = CreateStore(fakeClient);
		var payload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
		const string path = "2026/area10e-14u/scorecards/14u-boys/10/photo.png";

		Assert.False(await store.ExistsAsync(path));

		await using (var stream = new MemoryStream(payload))
		{
			await store.UploadAsync(path, stream, "image/png");
		}

		Assert.True(await store.ExistsAsync(path));
		Assert.Equal("image/png", fakeClient.GetStoredContentType(TestBucket, path));

		await using (var downloaded = await store.OpenReadAsync(path))
		{
			using var buffer = new MemoryStream();
			await downloaded.CopyToAsync(buffer);
			Assert.Equal(payload, buffer.ToArray());
		}

		await store.DeleteAsync(path);
		await store.DeleteAsync(path);
		Assert.False(await store.ExistsAsync(path));
	}

	[Fact]
	public void UploadExistsOpenReadDelete_Sync_RoundTripsExpectedContent()
	{
		var fakeClient = new FakeStorageClient();
		var store = CreateStore(fakeClient);
		var payload = new byte[] { 10, 20, 30, 40 };
		const string path = "2026/area10e-14u/scorecards/14u-girls/15/photo.jpg";

		Assert.False(store.Exists(path));

		using (var stream = new MemoryStream(payload))
		{
			store.Upload(path, stream, "image/jpeg");
		}

		Assert.True(store.Exists(path));
		Assert.Equal("image/jpeg", fakeClient.GetStoredContentType(TestBucket, path));

		using (var downloaded = store.OpenRead(path))
		{
			using var buffer = new MemoryStream();
			downloaded.CopyTo(buffer);
			Assert.Equal(payload, buffer.ToArray());
		}

		store.Delete(path);
		store.Delete(path);
		Assert.False(store.Exists(path));
	}

	[Fact]
	public async Task OpenRead_WhenObjectMissing_ThrowsFileNotFoundException()
	{
		var fakeClient = new FakeStorageClient();
		var store = CreateStore(fakeClient);

		Assert.Throws<FileNotFoundException>(() => store.OpenRead("missing/card.jpg"));
		await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync("missing/card.jpg"));
	}

	[Fact]
	public async Task UploadAsync_WhenTransientRateLimitOccurs_RewindsStreamAndRetries()
	{
		var fakeClient = new FakeStorageClient { TransientUploadFailuresRemaining = 1 };
		var store = CreateStore(fakeClient);
		var payload = new byte[] { 1, 3, 5, 7, 9, 11 };
		const string path = "2026/area10e-14u/scorecards/14u-boys/99/retry.webp";

		await using var stream = new MemoryStream(payload);
		await store.UploadAsync(path, stream, "image/webp");

		Assert.Equal(2, fakeClient.UploadCallCount);

		await using var downloaded = await store.OpenReadAsync(path);
		using var copy = new MemoryStream();
		await downloaded.CopyToAsync(copy);
		Assert.Equal(payload, copy.ToArray());
	}

	private static GcsBlobObjectStore CreateStore(StorageClient client)
	{
		return new GcsBlobObjectStore(
			client,
			Options.Create(new StorageOptions { Provider = "Gcs", BucketName = TestBucket }));
	}

	private sealed class FakeStorageClient : StorageClient
	{
		private readonly Dictionary<(string Bucket, string Name), (byte[] Bytes, string ContentType)> _objects = new();

		public int TransientUploadFailuresRemaining { get; set; }

		public int UploadCallCount { get; private set; }

		public string GetStoredContentType(string bucket, string objectName)
		{
			return _objects[(bucket, objectName)].ContentType;
		}

		public override GcsObject UploadObject(
			string bucket,
			string objectName,
			string? contentType,
			Stream source,
			UploadObjectOptions? options = null,
			IProgress<IUploadProgress>? progress = null)
		{
			UploadCallCount++;
			using var copy = new MemoryStream();
			source.CopyTo(copy);

			if (TransientUploadFailuresRemaining > 0)
			{
				TransientUploadFailuresRemaining--;
				throw CreateGoogleApiException(HttpStatusCode.TooManyRequests);
			}

			_objects[(bucket, objectName)] = (copy.ToArray(), contentType ?? "application/octet-stream");
			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = contentType, Generation = 1 };
		}

		public override async Task<GcsObject> UploadObjectAsync(
			string bucket,
			string objectName,
			string? contentType,
			Stream source,
			UploadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<IUploadProgress>? progress = null)
		{
			UploadCallCount++;
			await using var copy = new MemoryStream();
			await source.CopyToAsync(copy, cancellationToken);

			if (TransientUploadFailuresRemaining > 0)
			{
				TransientUploadFailuresRemaining--;
				throw CreateGoogleApiException(HttpStatusCode.TooManyRequests);
			}

			_objects[(bucket, objectName)] = (copy.ToArray(), contentType ?? "application/octet-stream");
			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = contentType, Generation = 1 };
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
				throw CreateGoogleApiException(HttpStatusCode.NotFound);
			}

			destination.Write(entry.Bytes, 0, entry.Bytes.Length);
			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = entry.ContentType, Generation = 1 };
		}

		public override async Task<GcsObject> DownloadObjectAsync(
			string bucket,
			string objectName,
			Stream destination,
			DownloadObjectOptions? options = null,
			CancellationToken cancellationToken = default,
			IProgress<Google.Apis.Download.IDownloadProgress>? progress = null)
		{
			if (!_objects.TryGetValue((bucket, objectName), out var entry))
			{
				throw CreateGoogleApiException(HttpStatusCode.NotFound);
			}

			await destination.WriteAsync(entry.Bytes, cancellationToken);
			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = entry.ContentType, Generation = 1 };
		}

		public override GcsObject GetObject(string bucket, string objectName, GetObjectOptions? options = null)
		{
			if (!_objects.TryGetValue((bucket, objectName), out var entry))
			{
				throw CreateGoogleApiException(HttpStatusCode.NotFound);
			}

			return new GcsObject { Bucket = bucket, Name = objectName, ContentType = entry.ContentType, Generation = 1 };
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
			if (!_objects.Remove((bucket, objectName)))
			{
				throw CreateGoogleApiException(HttpStatusCode.NotFound);
			}
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

		private static GoogleApiException CreateGoogleApiException(HttpStatusCode statusCode)
		{
			return new GoogleApiException("storage", statusCode.ToString())
			{
				HttpStatusCode = statusCode,
			};
		}
	}
}
