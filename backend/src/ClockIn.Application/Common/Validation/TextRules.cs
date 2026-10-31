using ClockIn.Domain.Timesheets;
using FluentValidation;

namespace ClockIn.Application.Common.Validation;

public static class TextRules
{
    /// <summary>Required, not blank, at most <see cref="TimeEntry.MaxNoteLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> MustBeNote<T>(this IRuleBuilder<T, string> rule) =>
        rule.MustBeText(TimeEntry.MaxNoteLength);

    /// <summary>Required, not blank, at most <see cref="Timesheet.MaxCommentLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> MustBeComment<T>(this IRuleBuilder<T, string> rule) =>
        rule.MustBeText(Timesheet.MaxCommentLength);

    private static IRuleBuilderOptions<T, string> MustBeText<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.Must(text => !string.IsNullOrWhiteSpace(text)).WithMessage("Enter some text.")
            .Must(text => (text ?? "").Trim().Length <= maxLength).WithMessage($"Use at most {maxLength} characters.");
}
