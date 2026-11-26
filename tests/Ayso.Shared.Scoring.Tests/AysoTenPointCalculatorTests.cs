using Ayso.Shared.Scoring;
using Ayso.Shared.Scoring.Models;

namespace Ayso.Shared.Scoring.Tests;

public class AysoTenPointCalculatorTests
{
	[Fact]
	public void Calculate_WinEarnsSixPlusGoalBonusPlusShutout()
	{
		var teams = new List<(int, string, int, bool)>
		{
			(1, "Home", 1, false),
			(2, "Away", 2, false),
		};

		var matches = new List<MatchResult>
		{
			new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 2, AwayGoals = 0 },
		};

		var result = AysoTenPointCalculator.Calculate(teams, matches);

		var home = result.Standings.Single(s => s.TeamId == 1);
		// 6 win + 2 goal bonus + 1 shutout = 9
		Assert.Equal(9, home.Points);
		Assert.Equal(1, home.Wins);
	}

	[Fact]
	public void Calculate_ForfeitWinnerGetsSevenWithoutShutoutBonus()
	{
		var teams = new List<(int, string, int, bool)>
		{
			(1, "Home", 1, false),
			(2, "Away", 2, false),
		};

		var matches = new List<MatchResult>
		{
			new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 1, AwayGoals = 0, IsForfeit = true },
		};

		var result = AysoTenPointCalculator.Calculate(teams, matches);
		Assert.Equal(7, result.Standings.Single(s => s.TeamId == 1).Points);
		Assert.True(result.AnyForfeitInScope);
	}

	[Fact]
	public void Calculate_AppliesCardDeductions()
	{
		var teams = new List<(int, string, int, bool)>
		{
			(1, "Home", 1, false),
			(2, "Away", 2, false),
		};

		var matches = new List<MatchResult>
		{
			new()
			{
				HomeTeamId = 1,
				AwayTeamId = 2,
				HomeGoals = 1,
				AwayGoals = 0,
				HomeYellowCards = 1,
				HomeRedCards = 1,
			},
		};

		var result = AysoTenPointCalculator.Calculate(teams, matches);
		// 6 + 1 goal + 1 shutout - 1 YC - 2 RC = 5
		Assert.Equal(5, result.Standings.Single(s => s.TeamId == 1).Points);
	}

	[Fact]
	public void Calculate_AppliesOptionalVolunteerDeduction()
	{
		var teams = new List<(int, string, int, bool)>
		{
			(1, "Home", 1, true),
			(2, "Away", 2, false),
		};

		var matches = new List<MatchResult>
		{
			new() { HomeTeamId = 1, AwayTeamId = 2, HomeGoals = 0, AwayGoals = 0 },
		};

		var result = AysoTenPointCalculator.Calculate(teams, matches, applyVolunteerDeduction: true);
		var home = result.Standings.Single(s => s.TeamId == 1);
		// draw 3 + 0 goals + shutout 1 - volunteer 1 = 3
		Assert.Equal(3, home.Points);
		Assert.True(home.HasVolunteerPointDeduction);
	}
}