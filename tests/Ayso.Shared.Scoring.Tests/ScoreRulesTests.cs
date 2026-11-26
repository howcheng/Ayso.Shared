using Ayso.Shared.Scoring;

namespace Ayso.Shared.Scoring.Tests;

public class ScoreRulesTests
{
	[Fact]
	public void ValidateNonNegativeGoals_RejectsNegative()
	{
		Assert.Throws<ArgumentException>(() => ScoreRules.ValidateNonNegativeGoals(-1, 0));
		Assert.Throws<ArgumentException>(() => ScoreRules.ValidateNonNegativeGoals(0, -1));
	}

	[Theory]
	[InlineData(1, 0)]
	[InlineData(0, 1)]
	public void ValidateForfeitScore_AllowsOneNil(int home, int away)
	{
		ScoreRules.ValidateForfeitScore(isForfeit: true, home, away);
	}

	[Fact]
	public void ValidateForfeitScore_RejectsNonOneNil()
	{
		Assert.Throws<ArgumentException>(() => ScoreRules.ValidateForfeitScore(true, 2, 0));
	}

	[Fact]
	public void ValidateEliminationScore_TiedRequiresPenalties()
	{
		Assert.Throws<ArgumentException>(() =>
			ScoreRules.ValidateEliminationScore(1, 1, isForfeit: false, isDecidedByPenalties: false, null, null));
	}

	[Fact]
	public void ValidateEliminationScore_ForfeitCannotHavePenalties()
	{
		Assert.Throws<ArgumentException>(() =>
			ScoreRules.ValidateEliminationScore(1, 0, isForfeit: true, isDecidedByPenalties: true, 3, 2));
	}

	[Fact]
	public void ValidateEliminationScore_TiedWithDistinctPenalties_Succeeds()
	{
		ScoreRules.ValidateEliminationScore(1, 1, isForfeit: false, isDecidedByPenalties: true, 4, 3);
	}
}