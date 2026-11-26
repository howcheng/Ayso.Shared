namespace Ayso.Shared.Scoring.Models;

public sealed class SoccerStandingsResult
{
	public IReadOnlyList<TeamStandingRow> Standings { get; init; } = Array.Empty<TeamStandingRow>();

	public bool UsesPointsPerGame { get; init; }
}