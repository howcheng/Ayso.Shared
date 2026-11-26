using Ayso.Shared.Scoring.Models;

namespace Ayso.Shared.Scoring;

/// <summary>
/// AYSO 10-point pool-play standings calculator with the standard tiebreaker chain.
/// Optional volunteer deduction and KFTM overrides are parameterized for host apps.
/// </summary>
public static class AysoTenPointCalculator
{
	public const int WinPoints = 6;
	public const int DrawPoints = 3;
	public const int LossPoints = 0;
	public const int MaxGoalBonusPerGame = 3;
	public const int ShutoutBonusPoints = 1;
	public const int ForfeitWinPoints = 7;
	public const int RedCardDeduction = 2;
	public const int YellowCardDeduction = 1;
	public const int VolunteerPointDeduction = 1;

	/// <summary>
	/// Calculates standings for a set of teams and completed matches in one pool/scope.
	/// </summary>
	/// <param name="teams">Teams in scope. Item4 is whether the team receives the host volunteer deduction.</param>
	/// <param name="completedMatches">Completed matches (both goal values set).</param>
	/// <param name="kftmOverrides">Optional KFTM overrides among tied teams.</param>
	/// <param name="expectedGameCount">When null, derived as n*(n-1)/2 from <paramref name="teams"/>.</param>
	/// <param name="anyForfeitInScope">When set, overrides local forfeit detection for wins/GD nullification.</param>
	/// <param name="applyVolunteerDeduction">When true, teams flagged for deduction lose <see cref="VolunteerPointDeduction"/>.</param>
	public static AysoTenPointStandingsResult Calculate(
		IReadOnlyList<(int TeamId, string TeamName, int TeamNumber, bool ApplyVolunteerDeduction)> teams,
		IReadOnlyList<MatchResult> completedMatches,
		IReadOnlyList<KftmOverride>? kftmOverrides = null,
		int? expectedGameCount = null,
		bool? anyForfeitInScope = null,
		bool applyVolunteerDeduction = false)
	{
		ArgumentNullException.ThrowIfNull(teams);
		ArgumentNullException.ThrowIfNull(completedMatches);

		var forfeitInScope = anyForfeitInScope ?? completedMatches.Any(m => m.IsForfeit);
		var expectedCount = expectedGameCount ?? (teams.Count * (teams.Count - 1) / 2);
		var isFinal = expectedCount > 0 && completedMatches.Count >= expectedCount;
		var overrides = kftmOverrides ?? Array.Empty<KftmOverride>();

		var rows = teams
			.Select(team => CalculateTeamStanding(
				team.TeamId,
				team.TeamName,
				team.TeamNumber,
				applyVolunteerDeduction && team.ApplyVolunteerDeduction,
				completedMatches))
			.ToList();

		var (sortedRows, requiresKftm, tiedTeamIds) = SortWithTiebreakers(
			rows,
			completedMatches,
			forfeitInScope,
			isFinal,
			overrides);

		for (var i = 0; i < sortedRows.Count; i++)
		{
			sortedRows[i].Rank = i + 1;
		}

		return new AysoTenPointStandingsResult
		{
			Standings = sortedRows,
			AnyForfeitInScope = forfeitInScope,
			IsFinal = isFinal,
			RequiresKftmTiebreaker = requiresKftm,
			TiedTeamIdsForKftm = tiedTeamIds,
		};
	}

	private static TeamStandingRow CalculateTeamStanding(
		int teamId,
		string teamName,
		int teamNumber,
		bool applyVolunteerDeduction,
		IReadOnlyList<MatchResult> completedMatches)
	{
		var dto = new TeamStandingRow
		{
			TeamId = teamId,
			TeamName = teamName,
			TeamNumber = teamNumber,
			HasVolunteerPointDeduction = applyVolunteerDeduction,
		};

		if (applyVolunteerDeduction)
		{
			dto.Points -= VolunteerPointDeduction;
		}

		var teamMatches = completedMatches
			.Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
			.ToList();

		dto.GamesPlayed = teamMatches.Count;

		foreach (var match in teamMatches)
		{
			var isHome = match.HomeTeamId == teamId;
			var goalsFor = isHome ? match.HomeGoals : match.AwayGoals;
			var goalsAgainst = isHome ? match.AwayGoals : match.HomeGoals;
			var redCards = isHome ? match.HomeRedCards : match.AwayRedCards;
			var yellowCards = isHome ? match.HomeYellowCards : match.AwayYellowCards;

			dto.GoalsFor += goalsFor;
			dto.GoalsAgainst += goalsAgainst;
			dto.RedCards += redCards;
			dto.YellowCards += yellowCards;

			if (match.IsForfeit)
			{
				dto.HasForfeit = true;

				if (goalsFor > goalsAgainst)
				{
					dto.Wins++;
					dto.Points += ForfeitWinPoints;
				}
				else
				{
					dto.Losses++;
				}
			}
			else
			{
				if (goalsFor > goalsAgainst)
				{
					dto.Wins++;
					dto.Points += WinPoints;
				}
				else if (goalsFor == goalsAgainst)
				{
					dto.Draws++;
					dto.Points += DrawPoints;
				}
				else
				{
					dto.Losses++;
					dto.Points += LossPoints;
				}

				dto.Points += Math.Min(goalsFor, MaxGoalBonusPerGame);

				if (goalsAgainst == 0)
				{
					dto.Points += ShutoutBonusPoints;
				}

				var cappedFor = Math.Min(goalsFor, MaxGoalBonusPerGame);
				var cappedAgainst = Math.Min(goalsAgainst, MaxGoalBonusPerGame);
				dto.CappedGoalsAgainst += cappedAgainst;
				dto.CappedGoalDifferential += cappedFor - cappedAgainst;
			}

			dto.Points -= (redCards * RedCardDeduction) + (yellowCards * YellowCardDeduction);
		}

		dto.GoalDifferential = dto.GoalsFor - dto.GoalsAgainst;
		return dto;
	}

	private static (List<TeamStandingRow> Sorted, bool RequiresKftm, List<int> TiedTeamIds) SortWithTiebreakers(
		List<TeamStandingRow> rows,
		IReadOnlyList<MatchResult> completedMatches,
		bool anyForfeitInScope,
		bool allPoolGamesCompleted,
		IReadOnlyList<KftmOverride> kftmOverrides)
	{
		var sorted = new List<TeamStandingRow>(rows.Count);
		var statisticalSignatureByTeamId = new Dictionary<int, (int Points, int H2H, int EffectiveWins, int RedCards, int YellowCards, int CappedGA, int EffectiveCappedGD)>();

		foreach (var group in rows.GroupBy(r => r.Points).OrderByDescending(g => g.Key))
		{
			var groupTeams = group.ToList();
			if (groupTeams.Count == 1)
			{
				var single = groupTeams[0];
				statisticalSignatureByTeamId[single.TeamId] = BuildSignature(single, h2h: 0, anyForfeitInScope);
				sorted.Add(single);
				continue;
			}

			var h2hScoreByTeamId = groupTeams.ToDictionary(t => t.TeamId, _ => 0);
			if (groupTeams.Count == 2)
			{
				ApplyHeadToHead(groupTeams[0].TeamId, groupTeams[1].TeamId, completedMatches, h2hScoreByTeamId);
			}

			foreach (var team in groupTeams)
			{
				statisticalSignatureByTeamId[team.TeamId] = BuildSignature(
					team,
					h2hScoreByTeamId[team.TeamId],
					anyForfeitInScope);
			}

			var tiedIds = groupTeams.Select(t => t.TeamId).ToList();
			var orderedGroup = groupTeams
				.OrderByDescending(t => h2hScoreByTeamId[t.TeamId])
				.ThenByDescending(t => anyForfeitInScope ? 0 : t.Wins)
				.ThenBy(t => t.RedCards)
				.ThenBy(t => t.YellowCards)
				.ThenBy(t => t.CappedGoalsAgainst)
				.ThenByDescending(t => anyForfeitInScope ? 0 : t.CappedGoalDifferential)
				.ThenByDescending(t => CountKftmWinsInGroup(t.TeamId, tiedIds, kftmOverrides))
				.ThenBy(t => t.TeamNumber)
				.ThenBy(t => t.TeamName, StringComparer.OrdinalIgnoreCase)
				.ToList();

			sorted.AddRange(orderedGroup);
		}

		var requiresKftm = false;
		var tiedTeamIds = new List<int>();

		if (allPoolGamesCompleted && sorted.Count >= 2)
		{
			var unresolvedGroups = sorted
				.Select((team, index) => (Team: team, Rank: index + 1, Signature: statisticalSignatureByTeamId[team.TeamId]))
				.GroupBy(x => x.Signature)
				.Where(g => g.Count() > 1)
				.ToList();

			foreach (var sigGroup in unresolvedGroups)
			{
				var groupTeamIds = sigGroup.Select(x => x.Team.TeamId).ToList();
				var kftmResolved = groupTeamIds
					.Select(id => CountKftmWinsInGroup(id, groupTeamIds, kftmOverrides))
					.Distinct()
					.Count() > 1;

				if (!kftmResolved)
				{
					requiresKftm = true;
					tiedTeamIds.AddRange(groupTeamIds);
				}
			}
		}

		return (sorted, requiresKftm, tiedTeamIds.Distinct().ToList());
	}

	private static void ApplyHeadToHead(
		int teamAId,
		int teamBId,
		IReadOnlyList<MatchResult> completedMatches,
		Dictionary<int, int> h2hScoreByTeamId)
	{
		var h2hMatch = completedMatches.FirstOrDefault(m =>
			(m.HomeTeamId == teamAId && m.AwayTeamId == teamBId) ||
			(m.HomeTeamId == teamBId && m.AwayTeamId == teamAId));

		if (h2hMatch is null)
		{
			return;
		}

		if (h2hMatch.HomeGoals > h2hMatch.AwayGoals)
		{
			h2hScoreByTeamId[h2hMatch.HomeTeamId] = 1;
		}
		else if (h2hMatch.AwayGoals > h2hMatch.HomeGoals)
		{
			h2hScoreByTeamId[h2hMatch.AwayTeamId] = 1;
		}
	}

	private static (int Points, int H2H, int EffectiveWins, int RedCards, int YellowCards, int CappedGA, int EffectiveCappedGD) BuildSignature(
		TeamStandingRow team,
		int h2h,
		bool anyForfeitInScope) =>
		(
			team.Points,
			h2h,
			anyForfeitInScope ? 0 : team.Wins,
			team.RedCards,
			team.YellowCards,
			team.CappedGoalsAgainst,
			anyForfeitInScope ? 0 : team.CappedGoalDifferential);

	private static int CountKftmWinsInGroup(
		int teamId,
		IReadOnlyList<int> tiedTeamIds,
		IReadOnlyList<KftmOverride> kftmOverrides)
	{
		if (kftmOverrides.Count == 0)
		{
			return 0;
		}

		return kftmOverrides.Count(o =>
			o.WinningTeamId == teamId &&
			tiedTeamIds.Contains(o.TeamAId) &&
			tiedTeamIds.Contains(o.TeamBId) &&
			(o.TeamAId == teamId || o.TeamBId == teamId));
	}
}