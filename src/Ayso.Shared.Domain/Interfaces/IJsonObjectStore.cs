using Ayso.Shared.Domain.Documents;

namespace Ayso.Shared.Domain.Interfaces;

/// <summary>
/// Low-level JSON document persistence by object path, with optimistic concurrency via generation.
/// App-specific stores wrap this with typed document paths.
/// </summary>
public interface IJsonObjectStore
{
	Task<StorageDocument<T>> GetAsync<T>(string objectPath, CancellationToken cancellationToken = default);

	Task SaveAsync<T>(string objectPath, T document, long? expectedGeneration, CancellationToken cancellationToken = default);

	Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken = default);

	Task DeleteAsync(string objectPath, long? expectedGeneration = null, CancellationToken cancellationToken = default);
}