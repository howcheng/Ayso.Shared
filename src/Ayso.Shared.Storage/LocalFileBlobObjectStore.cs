using Ayso.Shared.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IBlobObjectStore"/> using atomic <c>.tmp</c> renames.
/// </summary>
public class LocalFileBlobObjectStore : IBlobObjectStore
{
	private const int DefaultBufferSize = 81920;

	private readonly string _rootPath;
	private readonly string _rootPathWithSeparator;
	private readonly object _syncRoot = new();

	public LocalFileBlobObjectStore(IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_rootPath = Path.GetFullPath(options.Value.LocalRoot);
		_rootPathWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
			? _rootPath
			: _rootPath + Path.DirectorySeparatorChar;
		Directory.CreateDirectory(_rootPath);
	}

	public void Upload(string objectPath, Stream content, string contentType)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		var fullPath = GetFullPath(objectPath);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

		var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: false))
			{
				content.CopyTo(fileStream, DefaultBufferSize);
			}

			CommitTempFile(tempPath, fullPath);
		}
		catch
		{
			TryDeleteTempFile(tempPath);
			throw;
		}
	}

	public Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(content);
		ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

		var fullPath = GetFullPath(objectPath);
		return UploadInternalAsync(fullPath, content, cancellationToken);
	}

	private async Task UploadInternalAsync(string fullPath, Stream content, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

		var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, DefaultBufferSize, useAsync: true))
			{
				await content.CopyToAsync(fileStream, DefaultBufferSize, cancellationToken);
			}

			CommitTempFile(tempPath, fullPath);
		}
		catch
		{
			TryDeleteTempFile(tempPath);
			throw;
		}
	}

	private void CommitTempFile(string tempPath, string fullPath)
	{
		lock (_syncRoot)
		{
			File.Move(tempPath, fullPath, overwrite: true);
		}
	}

	private static void TryDeleteTempFile(string tempPath)
	{
		try
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
		catch (IOException)
		{
		}
	}

	private string GetFullPath(string objectPath)
	{
		if (objectPath.StartsWith('/') || objectPath.StartsWith('\\') || Path.IsPathRooted(objectPath))
		{
			throw new ArgumentException($"Object path '{objectPath}' must be a relative path.", nameof(objectPath));
		}

		var relativePath = objectPath
			.Replace('/', Path.DirectorySeparatorChar)
			.Replace('\\', Path.DirectorySeparatorChar);

		var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
		if (!fullPath.StartsWith(_rootPathWithSeparator, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException($"Object path '{objectPath}' resolves outside the storage root.", nameof(objectPath));
		}

		return fullPath;
	}

	public Stream OpenRead(string objectPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return OpenReadInternal(objectPath, useAsync: false);
	}

	public Task<Stream> OpenReadAsync(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(OpenReadInternal(objectPath, useAsync: true));
	}

	private Stream OpenReadInternal(string objectPath, bool useAsync)
	{
		var fullPath = GetFullPath(objectPath);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException($"Storage object not found: {objectPath}", fullPath);
		}

		try
		{
			return new FileStream(
				fullPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read | FileShare.Delete,
				DefaultBufferSize,
				useAsync);
		}
		catch (DirectoryNotFoundException ex)
		{
			throw new FileNotFoundException($"Storage object not found: {objectPath}", fullPath, ex);
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
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(ExistsInternal(objectPath));
	}

	private bool ExistsInternal(string objectPath)
	{
		var fullPath = GetFullPath(objectPath);
		lock (_syncRoot)
		{
			return File.Exists(fullPath);
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
		cancellationToken.ThrowIfCancellationRequested();
		DeleteInternal(objectPath);
		return Task.CompletedTask;
	}

	private void DeleteInternal(string objectPath)
	{
		var fullPath = GetFullPath(objectPath);
		lock (_syncRoot)
		{
			if (File.Exists(fullPath))
			{
				File.Delete(fullPath);
			}
		}
	}
}
