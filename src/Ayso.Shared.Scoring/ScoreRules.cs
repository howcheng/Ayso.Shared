namespace Ayso.Shared.Scoring;

/// <summary>
/// Shared validation rules for score entry across league and tournament apps.
/// </summary>
public static class ScoreRules
{
	public static void ValidateNonNegativeGoals(int homeGoals, int awayGoals)
	{
		if (homeGoals < 0)
		{
			throw new ArgumentException("Home score cannot be negative.", nameof(homeGoals));
		}

		if (awayGoals < 0)
		{
			throw new ArgumentException("Away score cannot be negative.", nameof(awayGoals));
		}
	}

	public static void ValidateNonNegativeCards(
		int homeRedCards,
		int awayRedCards,
		int homeYellowCards,
		int awayYellowCards)
	{
		if (homeRedCards < 0 || awayRedCards < 0 || homeYellowCards < 0 || awayYellowCards < 0)
		{
			throw new ArgumentException("Red and yellow card counts cannot be negative.");
		}
	}

	public static void ValidateForfeitScore(bool isForfeit, int homeGoals, int awayGoals)
	{
		if (!isForfeit)
		{
			return;
		}

		if (!((homeGoals == 1 && awayGoals == 0) || (homeGoals == 0 && awayGoals == 1)))
		{
			throw new ArgumentException("Forfeit games must be scored as 1-0 or 0-1.");
		}
	}

	public static void ValidatePoolPlayScore(
		int homeGoals,
		int awayGoals,
		bool isForfeit,
		int homeRedCards = 0,
		int awayRedCards = 0,
		int homeYellowCards = 0,
		int awayYellowCards = 0)
	{
		ValidateNonNegativeGoals(homeGoals, awayGoals);
		ValidateNonNegativeCards(homeRedCards, awayRedCards, homeYellowCards, awayYellowCards);
		ValidateForfeitScore(isForfeit, homeGoals, awayGoals);
	}

	public static void ValidateEliminationScore(
		int homeGoals,
		int awayGoals,
		bool isForfeit,
		bool isDecidedByPenalties,
		int? homePenaltyScore,
		int? awayPenaltyScore)
	{
		ValidateNonNegativeGoals(homeGoals, awayGoals);

		if (isForfeit)
		{
			ValidateForfeitScore(isForfeit: true, homeGoals, awayGoals);

			if (isDecidedByPenalties || homePenaltyScore.HasValue || awayPenaltyScore.HasValue)
			{
				throw new ArgumentException("Forfeit games cannot also have penalty shootout scores.");
			}

			return;
		}

		if (homeGoals == awayGoals)
		{
			if (!homePenaltyScore.HasValue || !awayPenaltyScore.HasValue)
			{
				throw new ArgumentException(
					"Elimination games that end in a tie in regulation require both penalty shootout scores.");
			}

			if (homePenaltyScore.Value < 0 || awayPenaltyScore.Value < 0)
			{
				throw new ArgumentException("Penalty shootout scores cannot be negative.");
			}

			if (homePenaltyScore.Value == awayPenaltyScore.Value)
			{
				throw new ArgumentException("Penalty shootout scores cannot be tied; a winner must be decided.");
			}
		}
		else if (isDecidedByPenalties || homePenaltyScore.HasValue || awayPenaltyScore.HasValue)
		{
			throw new ArgumentException(
				"Penalty shootout scores are only allowed when regulation ends in a tie.");
		}
	}
}