namespace Ayso.Shared.Scoring.Models;

/// <summary>
/// Neutral representation of a completed match result for standings calculators.
/// </summary>
public sealed class MatchResult
{
	public required int HomeTeamId { get; init; }

	public required int AwayTeamId { get; init; }

	public required int HomeGoals { get; init; }

	public required int AwayGoals { get; init; }

	public bool IsForfeit { get; init; }

	public int HomeRedCards { get; init; }

	public int AwayRedCards { get; init; }

	public int HomeYellowCards { get; init; }

	public int AwayYellowCards { get; init; }
}