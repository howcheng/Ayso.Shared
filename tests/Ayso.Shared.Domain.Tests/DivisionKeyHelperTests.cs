using Ayso.Shared.Domain.Enums;
using Ayso.Shared.Domain.Helpers;

namespace Ayso.Shared.Domain.Tests;

public class DivisionKeyHelperTests
{
	[Theory]
	[InlineData(AgeGroup.U10, Gender.Boys, "10u-boys")]
	[InlineData(AgeGroup.U12, Gender.Girls, "12u-girls")]
	[InlineData(AgeGroup.U14, Gender.Boys, "14u-boys")]
	public void ToKey_ReturnsCanonicalKey(AgeGroup ageGroup, Gender gender, string expected)
	{
		Assert.Equal(expected, DivisionKeyHelper.ToKey(ageGroup, gender));
	}

	[Theory]
	[InlineData("10UB", AgeGroup.U10, Gender.Boys)]
	[InlineData("12UG", AgeGroup.U12, Gender.Girls)]
	[InlineData("14U Boys", AgeGroup.U14, Gender.Boys)]
	[InlineData("10U-Girls", AgeGroup.U10, Gender.Girls)]
	public void ParseDivisionName_SupportsCompactAndSpacedForms(string input, AgeGroup age, Gender gender)
	{
		var parsed = DivisionKeyHelper.ParseDivisionName(input);
		Assert.Equal(age, parsed.AgeGroup);
		Assert.Equal(gender, parsed.Gender);
	}

	[Fact]
	public void ToShortCode_ReturnsCgiStyleCode()
	{
		Assert.Equal("14UB", DivisionKeyHelper.ToShortCode(AgeGroup.U14, Gender.Boys));
		Assert.Equal("10UG", DivisionKeyHelper.ToShortCode(AgeGroup.U10, Gender.Girls));
	}

	[Fact]
	public void GetDivisionObjectPath_IncludesYearSlugAndKey()
	{
		var path = DivisionKeyHelper.GetDivisionObjectPath(2026, "area-10e", "14u-boys");
		Assert.Equal("2026/area-10e/14u-boys.json", path);
	}

	[Fact]
	public void GetLockObjectPath_UsesSlugPrefix()
	{
		var path = DivisionKeyHelper.GetLockObjectPath("import", "panther-shootout");
		Assert.Equal("panther-shootout/locks/import.json", path);
	}
}