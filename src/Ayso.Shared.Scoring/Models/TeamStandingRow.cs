namespace Ayso.Shared.Scoring.Models;

/// <summary>
/// Accumulated standing stats for a single team under either 3-1-0 or AYSO 10-point scoring.
/// </summary>
public sealed class TeamStandingRow
{
	public int Rank { get; set; }

	public required int TeamId { get; init; }

	public string TeamName { get; set; } = string.Empty;

	public string TeamShortName { get; set; } = string.Empty;

	public int TeamNumber { get; set; }

	public int GamesPlayed { get; set; }

	public int Wins { get; set; }

	public int Draws { get; set; }

	public int Losses { get; set; }

	/// <summary>
	/// Game points for 3-1-0, or AYSO 10-point pool points for tournament scoring.
	/// </summary>
	public int Points { get; set; }

	public int GoalsFor { get; set; }

	public int GoalsAgainst { get; set; }

	public int GoalDifferential { get; set; }

	public int CappedGoalsAgainst { get; set; }

	public int CappedGoalDifferential { get; set; }

	public int YellowCards { get; set; }

	public int RedCards { get; set; }

	public bool HasForfeit { get; set; }

	public bool HasVolunteerPointDeduction { get; set; }

	public decimal? PointsPerGame { get; set; }
}