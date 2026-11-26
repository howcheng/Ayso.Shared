namespace Ayso.Shared.Scoring.Models;

public sealed class AysoTenPointStandingsResult
{
	public IReadOnlyList<TeamStandingRow> Standings { get; init; } = Array.Empty<TeamStandingRow>();

	public bool AnyForfeitInScope { get; init; }

	public bool IsFinal { get; init; }

	public bool RequiresKftmTiebreaker { get; init; }

	public IReadOnlyList<int> TiedTeamIdsForKftm { get; init; } = Array.Empty<int>();
}