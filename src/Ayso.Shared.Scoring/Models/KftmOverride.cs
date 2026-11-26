namespace Ayso.Shared.Scoring.Models;

/// <summary>
/// Kicks-from-the-mark tiebreaker override between two teams in a tied standings group.
/// </summary>
public sealed class KftmOverride
{
	public required int TeamAId { get; init; }

	public required int TeamBId { get; init; }

	public required int WinningTeamId { get; init; }
}