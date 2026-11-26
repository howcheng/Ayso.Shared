using System.ComponentModel.DataAnnotations;

namespace Ayso.Shared.Domain.Enums;

public enum AgeGroup
{
	[Display(Name = "10U")]
	U10 = 0,
	[Display(Name = "12U")]
	U12 = 1,
	[Display(Name = "14U")]
	U14 = 2
}