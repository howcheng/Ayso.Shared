using Ayso.Shared.Scoring;
using Ayso.Shared.Scoring.Models;

namespace Ayso.Shared.Scoring.Tests;

public class SoccerPointsCalculatorTests
{
	[Fact]
	public void Calculate_AppliesWinDrawLossPointsAndH2H()
	{
		var teams = new List<(int, string, string)>
		{
			(1, "Alpha", "A"),
			(2, "Bravo", "B"),
			(3, "Charlie", "C"),
		};

		var matches = new List<MatchResult>
		{
			new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 1 },
			new() { HomeTeamId = 1, AwayTeamId = 3, HomeGoals = 1, AwayGoals = 1 },
			new() { HomeTeamId = 2, AwayTeamId = 3, HomeGoals = 0, AwayGoals = 1 },
		};

		var result = SoccerPointsCalculator.Calculate(teams, matches);

		Assert.Equal(3, result.Standings.Count);
		Assert.Equal(1, result.Standings[0].TeamId);
		Assert.Equal(4, result.Standings[0].Points); // W + D
		Assert.Equal(3, result.Standings[1].TeamId);
		Assert.Equal(4, result.Standings[1].Points); // D + W
		// H2H among 1 and 3: team 1 drew with 3, so GD/GF break remaining ties
		Assert.Equal(2, result.Standings[2].TeamId);
		Assert.Equal(0, result.Standings[2].Points);
	}

	[Fact]
	public void PointsForResult_MatchesThreeOneZero()
	{
		Assert.Equal(3, SoccerPointsCalculator.PointsForResult(2, 1));
		Assert.Equal(1, SoccerPointsCalculator.PointsForResult(1, 1));
		Assert.Equal(0, SoccerPointsCalculator.PointsForResult(0, 2));
	}

	[Fact]
	public void Calculate_ExposesPointsPerGameWhenGamesPlayedDiffer()
	{
		var teams = new List<(int, string, string)>
		{
			(1, "Alpha", "A"),
			(2, "Bravo", "B"),
		};

		var matches = new List<MatchResult>
		{
			new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0 },
		};

		// Only one match so both have GP=1; add a second team-only scenario by using three teams with uneven play
		teams.Add((3, "Charlie", "C"));
		matches.Add(new MatchResult { HomeTeamId = 1, AwayTeamId = 3, HomeGoals = 2, AwayGoals = 0 });

		var result = SoccerPointsCalculator.Calculate(teams, matches);

		Assert.True(result.UsesPointsPerGame);
		Assert.NotNull(result.Standings.First(s => s.TeamId == 1).PointsPerGame);
	}
}