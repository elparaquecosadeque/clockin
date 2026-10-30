using System.ComponentModel.DataAnnotations;

namespace ClockIn.Api.Common;

public sealed class CompanyOptions
{
    public const string Section = "Company";

    /// <summary>IANA id of the single company timezone (glossary: Company time). Deploy config only.</summary>
    [Required]
    public required string TimeZone { get; init; }
}
