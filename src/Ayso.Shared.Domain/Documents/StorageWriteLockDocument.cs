namespace Ayso.Shared.Domain.Documents;

public class StorageWriteLockDocument
{
	public string Holder { get; set; } = string.Empty;

	public DateTime AcquiredAtUtc { get; set; }

	public DateTime ExpiresAtUtc { get; set; }
}