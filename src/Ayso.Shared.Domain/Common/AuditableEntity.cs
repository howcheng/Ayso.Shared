namespace Ayso.Shared.Domain.Common;

/// <summary>
/// Base class for entities that need audit tracking plus a manually-incremented row version.
/// </summary>
public abstract class AuditableEntity
{
	public int Id { get; set; }

	public DateTime CreatedAt { get; set; }

	public string CreatedBy { get; set; } = string.Empty;

	public DateTime ModifiedAt { get; set; }

	public string ModifiedBy { get; set; } = string.Empty;

	/// <summary>
	/// Simple optimistic concurrency token, manually incremented by 1 on every save.
	/// </summary>
	public int RowVersion { get; set; } = 1;
}