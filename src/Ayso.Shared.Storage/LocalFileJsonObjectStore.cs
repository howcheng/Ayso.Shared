using System.Globalization;
using Ayso.Shared.Domain.Documents;
using Ayso.Shared.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace Ayso.Shared.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IJsonObjectStore"/> using atomic <c>.tmp</c> renames
/// and <c>.generation</c> sidecar files.
/// </summary>
public class LocalFileJsonObjectStore : IJsonObjectStore
{
	private const string GenerationSuffix = ".generation";

	private readonly string _rootPath;
	private readonly object _syncRoot = new();

	public LocalFileJsonObjectStore(IOptions<StorageOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_rootPath = Path.GetFullPath(options.Value.LocalRoot);
		Directory.CreateDirectory(_rootPath);
	}

	public StorageDocument<T> Get<T>(string objectPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		return ReadDocument<T>(objectPath);
	}

	public Task<StorageDocument<T>> GetAsync<T>(string objectPath, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(ReadDocument<T>(objectPath));
	}

	private StorageDocument<T> ReadDocument<T>(string objectPath)
	{
		lock (_syncRoot)
		{
			var fullPath = GetFullPath(objectPath);
			if (!File.Exists(fullPath))
			{
				throw new FileNotFoundException($"Storage object not found: {objectPath}", fullPath);
			}

			var json = File.ReadAllText(fullPath, StorageJsonSerializer.Utf8WithoutBom);
			var generation = ReadGeneration(objectPath);

			return new StorageDocument<T>
			{
				Content = StorageJsonSerializer.Deserialize<T>(json),
				Generation = generation,
				ObjectPath = objectPath,
			};
		}
	}

	private long? ReadGeneration(string objectPath)
	{
		var generationPath = GetGenerationPath(objectPath);
		if (!File.Exists(generationPath))
		{
			return File.Exists(GetFullPath(objectPath)) ? 1L : null;
		}

		var raw = File.ReadAllText(generationPath, StorageJsonSerializer.Utf8WithoutBom);
		return long.Parse(raw.Trim(), CultureInfo.InvariantCulture);
	}

	private string GetFullPath(string objectPath)
	{
		return Path.Combine(_rootPath, objectPath.Replace('/', Path.DirectorySeparatorChar));
	}

	private string GetGenerationPath(string objectPath)
	{
		return GetFullPath(objectPath + GenerationSuffix);
	}

	public void Save<T>(string objectPath, T document, long? expectedGeneration)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(document);
		WriteDocument(objectPath, document, expectedGeneration);
	}

	public Task SaveAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		ArgumentNullException.ThrowIfNull(document);
		cancellationToken.ThrowIfCancellationRequested();
		WriteDocument(objectPath, document, expectedGeneration);
		return Task.CompletedTask;
	}

	private void WriteDocument<T>(string objectPath, T document, long? expectedGeneration)
	{
		lock (_syncRoot)
		{
			var fullPath = GetFullPath(objectPath);
			var generationPath = GetGenerationPath(objectPath);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

			var currentGeneration = ReadGeneration(objectPath);
			EnsureExpectedGenerationMatches(objectPath, currentGeneration, expectedGeneration);

			var nextGeneration = (currentGeneration ?? 0L) + 1L;
			var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";

			File.WriteAllText(tempPath, StorageJsonSerializer.Serialize(document), StorageJsonSerializer.Utf8WithoutBom);
			File.Move(tempPath, fullPath, overwrite: true);
			File.WriteAllText(generationPath, nextGeneration.ToString(CultureInfo.InvariantCulture), StorageJsonSerializer.Utf8WithoutBom);
		}
	}

	private static void EnsureExpectedGenerationMatches(string objectPath, long? currentGeneration, long? expectedGeneration)
	{
		if (!expectedGeneration.HasValue)
		{
			return;
		}

		var matches = expectedGeneration.Value == 0
			? currentGeneration is null
			: currentGeneration == expectedGeneration.Value;

		if (!matches)
		{
			throw new StorageConcurrencyException(objectPath);
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
		lock (_syncRoot)
		{
			return File.Exists(GetFullPath(objectPath));
		}
	}

	public void Delete(string objectPath, long? expectedGeneration = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		DeleteInternal(objectPath, expectedGeneration);
	}

	public Task DeleteAsync(string objectPath, long? expectedGeneration = null, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(objectPath);
		cancellationToken.ThrowIfCancellationRequested();
		DeleteInternal(objectPath, expectedGeneration);
		return Task.CompletedTask;
	}

	private void DeleteInternal(string objectPath, long? expectedGeneration)
	{
		lock (_syncRoot)
		{
			var fullPath = GetFullPath(objectPath);
			var generationPath = GetGenerationPath(objectPath);
			var currentGeneration = ReadGeneration(objectPath);

			EnsureExpectedGenerationMatches(objectPath, currentGeneration, expectedGeneration);

			if (File.Exists(fullPath))
			{
				File.Delete(fullPath);
			}

			if (File.Exists(generationPath))
			{
				File.Delete(generationPath);
			}
		}
	}
}
