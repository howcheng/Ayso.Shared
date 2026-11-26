namespace Ayso.Shared.Storage;

/// <summary>
/// Configuration options for JSON document and binary blob storage (local filesystem or GCS).
/// </summary>
public class StorageOptions
{
	public const string SectionName = "Storage";

	public string Provider { get; set; } = "LocalFile";

	public string BucketName { get; set; } = "region42-storage";

	public string CompetitionSlug { get; set; } = string.Empty;

	public string LocalRoot { get; set; } = "../../.local-data/storage";
}