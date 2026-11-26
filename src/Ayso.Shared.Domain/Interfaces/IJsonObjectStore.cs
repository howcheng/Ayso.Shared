using Ayso.Shared.Domain.Documents;

namespace Ayso.Shared.Domain.Interfaces;

/// <summary>
/// Low-level JSON document persistence by object path, with optimistic concurrency via generation.
/// App-specific stores wrap this with typed document paths.
/// </summary>
public interface IJsonObjectStore
{
	StorageDocument<T> Get<T>(string objectPath);

	Task<StorageDocument<T>> GetAsync<T>(string objectPath, CancellationToken cancellationToken = default);

	void Save<T>(string objectPath, T document, long? expectedGeneration);

	Task SaveAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken = default);

	bool Exists(string objectPath);

	Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken = default);

	void Delete(string objectPath, long? expectedGeneration = null);

	Task DeleteAsync(string objectPath, long? expectedGeneration = null, CancellationToken cancellationToken = default);
}
