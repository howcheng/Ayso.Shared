namespace Ayso.Shared.Domain.Interfaces;

/// <summary>
/// Low-level binary blob persistence by object path (e.g., scorecard images).
/// App-specific services wrap this with typed paths and content validation.
/// </summary>
public interface IBlobObjectStore
{
	void Upload(string objectPath, Stream content, string contentType);

	Task UploadAsync(string objectPath, Stream content, string contentType, CancellationToken cancellationToken = default);

	Stream OpenRead(string objectPath);

	Task<Stream> OpenReadAsync(string objectPath, CancellationToken cancellationToken = default);

	bool Exists(string objectPath);

	Task<bool> ExistsAsync(string objectPath, CancellationToken cancellationToken = default);

	void Delete(string objectPath);

	Task DeleteAsync(string objectPath, CancellationToken cancellationToken = default);
}
