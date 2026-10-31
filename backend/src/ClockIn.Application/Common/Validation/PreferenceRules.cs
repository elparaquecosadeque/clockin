using ClockIn.Domain.Accounts;
using FluentValidation;

namespace ClockIn.Application.Common.Validation;

public static class PreferenceRules
{
    public static IRuleBuilderOptions<T, string> MustBeLanguage<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(Preferences.Languages.Contains).WithMessage($"Use one of: {string.Join(", ", Preferences.Languages)}.");

    public static IRuleBuilderOptions<T, string> MustBeTimeFormat<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(Preferences.TimeFormats.Contains).WithMessage($"Use one of: {string.Join(", ", Preferences.TimeFormats)}.");

    /// <summary>One of the <see cref="Role"/> names, exactly as written.</summary>
    public static IRuleBuilderOptions<T, string> MustBeRole<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(r => Enum.GetNames<Role>().Contains(r)).WithMessage($"Use one of: {string.Join(", ", Enum.GetNames<Role>())}.");
}
