using Ayso.Shared.Scoring.Models;

namespace Ayso.Shared.Scoring;

/// <summary>
/// Classic soccer 3-1-0 standings calculator (win/draw/loss). Callers filter teams and games
/// (e.g. region membership, scrimmage rounds, volunteer points) before invoking.
/// </summary>
public static class SoccerPointsCalculator
{
	public const int WinPoints = 3;
	public const int DrawPoints = 1;
	public const int LossPoints = 0;

	public static SoccerStandingsResult Calculate(
		IReadOnlyList<(int TeamId, string TeamName, string TeamShortName)> teams,
		IReadOnlyList<MatchResult> completedMatches)
	{
		ArgumentNullException.ThrowIfNull(teams);
		ArgumentNullException.ThrowIfNull(completedMatches);

		var rows = teams
			.Select(team => CalculateTeamStanding(team.TeamId, team.TeamName, team.TeamShortName, completedMatches))
			.ToList();

		var sorted = SortWithTiebreakers(rows, completedMatches);

		for (var i = 0; i < sorted.Count; i++)
		{
			sorted[i].Rank = i + 1;
		}

		var usesPpg = sorted.Select(s => s.GamesPlayed).Distinct().Count() > 1;
		if (usesPpg)
		{
			foreach (var standing in sorted)
			{
				standing.PointsPerGame = standing.GamesPlayed > 0
					? Math.Round((decimal)standing.Points / standing.GamesPlayed, 2)
					: 0m;
			}
		}

		return new SoccerStandingsResult
		{
			Standings = sorted,
			UsesPointsPerGame = usesPpg,
		};
	}

	public static int PointsForResult(int goalsFor, int goalsAgainst)
	{
		if (goalsFor > goalsAgainst)
		{
			return WinPoints;
		}

		if (goalsFor == goalsAgainst)
		{
			return DrawPoints;
		}

		return LossPoints;
	}

	public static Dictionary<int, int> CalculateHeadToHeadPoints(
		IReadOnlySet<int> tiedTeamIds,
		IReadOnlyList<MatchResult> completedMatches)
	{
		ArgumentNullException.ThrowIfNull(tiedTeamIds);
		ArgumentNullException.ThrowIfNull(completedMatches);

		var points = tiedTeamIds.ToDictionary(id => id, _ => 0);

		foreach (var match in completedMatches)
		{
			if (!tiedTeamIds.Contains(match.HomeTeamId) || !tiedTeamIds.Contains(match.AwayTeamId))
			{
				continue;
			}

			if (match.HomeGoals > match.AwayGoals)
			{
				points[match.HomeTeamId] += WinPoints;
			}
			else if (match.AwayGoals > match.HomeGoals)
			{
				points[match.AwayTeamId] += WinPoints;
			}
			else
			{
				points[match.HomeTeamId] += DrawPoints;
				points[match.AwayTeamId] += DrawPoints;
			}
		}

		return points;
	}

	private static TeamStandingRow CalculateTeamStanding(
		int teamId,
		string teamName,
		string teamShortName,
		IReadOnlyList<MatchResult> completedMatches)
	{
		var standing = new TeamStandingRow
		{
			TeamId = teamId,
			TeamName = teamName,
			TeamShortName = teamShortName,
		};

		var teamMatches = completedMatches
			.Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
			.ToList();

		standing.GamesPlayed = teamMatches.Count;

		foreach (var match in teamMatches)
		{
			var isHome = match.HomeTeamId == teamId;
			var goalsFor = isHome ? match.HomeGoals : match.AwayGoals;
			var goalsAgainst = isHome ? match.AwayGoals : match.HomeGoals;

			standing.GoalsFor += goalsFor;
			standing.GoalsAgainst += goalsAgainst;

			if (goalsFor > goalsAgainst)
			{
				standing.Wins++;
				standing.Points += WinPoints;
			}
			else if (goalsFor == goalsAgainst)
			{
				standing.Draws++;
				standing.Points += DrawPoints;
			}
			else
			{
				standing.Losses++;
			}
		}

		standing.GoalDifferential = standing.GoalsFor - standing.GoalsAgainst;
		return standing;
	}

	private static List<TeamStandingRow> SortWithTiebreakers(
		List<TeamStandingRow> standings,
		IReadOnlyList<MatchResult> completedMatches)
	{
		var sorted = new List<TeamStandingRow>(standings.Count);

		foreach (var group in standings
			.GroupBy(s => s.Points)
			.OrderByDescending(g => g.Key))
		{
			var groupTeams = group.ToList();
			if (groupTeams.Count == 1)
			{
				sorted.Add(groupTeams[0]);
				continue;
			}

			var tiedTeamIds = groupTeams.Select(t => t.TeamId).ToHashSet();
			var headToHeadPoints = CalculateHeadToHeadPoints(tiedTeamIds, completedMatches);

			var orderedGroup = groupTeams
				.OrderByDescending(s => headToHeadPoints.GetValueOrDefault(s.TeamId, 0))
				.ThenByDescending(s => s.GoalDifferential)
				.ThenByDescending(s => s.GoalsFor)
				.ThenBy(s => s.TeamName, StringComparer.OrdinalIgnoreCase)
				.ToList();

			sorted.AddRange(orderedGroup);
		}

		return sorted;
	}
}