using AwesomeAssertions;
using ClockIn.Api.Tests.Common;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Common.Interfaces;
using ClockIn.Application.Timesheets.Interfaces;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Api.Tests.Timesheets;

/// <summary>Saving and loading the Timesheet aggregate on the real database, including the constraints behind the punch rules.</summary>
[Collection(SharedApi.Name)]
public sealed class TimesheetRepositoryTests(ApiFactory factory) : IDisposable
{
    private static readonly PunchSource Phone = new("203.0.113.7", "Mobile Safari");
    private static readonly PunchSource Laptop = new("198.51.100.4", "Firefox");
    private static readonly Week LastWeek = new(new DateOnly(2026, 10, 19));
    private static readonly Week ThisWeek = new(new DateOnly(2026, 10, 26));

    private readonly UserId _eve = UserId.New();
    private readonly Scopes _scopes = new(factory);
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Loads_what_was_saved_when_the_week_is_found()
    {
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        timesheet.ClockOut(CompanyClock.At("2026-10-30 12:00"), Laptop);
        timesheet.ClockIn(CompanyClock.At("2026-10-30 13:00"), Phone, CompanyClock.Zone);
        await SaveOrUpdateAsync(timesheet);

        Timesheet? loaded = await Repository().FindAsync(_eve, ThisWeek, Ct);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(timesheet.Id);
        loaded.OwnerId.Should().Be(_eve);
        loaded.Week.Should().Be(ThisWeek);
        loaded.Status.Should().Be(TimesheetStatus.Draft);
        loaded.Version.Should().Be(1);
        loaded.Entries.Should().BeEquivalentTo(timesheet.Entries);
    }

    [Fact]
    public async Task Finds_nothing_when_the_week_has_no_timesheet()
    {
        Timesheet? loaded = await Repository().FindAsync(_eve, ThisWeek, Ct);

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Finds_the_open_entry_when_it_was_left_open_last_week()
    {
        var lastWeek = Timesheet.Start(_eve, LastWeek);
        lastWeek.ClockIn(CompanyClock.At("2026-10-25 22:00"), Phone, CompanyClock.Zone);
        await SaveOrUpdateAsync(lastWeek);

        Timesheet? found = await Repository().FindWithOpenEntryAsync(_eve, Ct);

        found!.Id.Should().Be(lastWeek.Id);
        found.OpenEntry!.Start.Should().Be(CompanyClock.At("2026-10-25 22:00"));
    }

    [Fact]
    public async Task Finds_no_open_entry_when_every_entry_is_closed()
    {
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        timesheet.ClockOut(CompanyClock.At("2026-10-30 12:00"), Laptop);
        await SaveOrUpdateAsync(timesheet);

        Timesheet? found = await Repository().FindWithOpenEntryAsync(_eve, Ct);

        found.Should().BeNull();
    }

    [Fact]
    public async Task Saves_changes_and_bumps_the_version_when_the_timesheet_exists()
    {
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        await SaveOrUpdateAsync(timesheet);

        Timesheet loaded = (await Repository().FindAsync(_eve, ThisWeek, Ct))!;
        loaded.ClockOut(CompanyClock.At("2026-10-30 12:00"), Laptop);
        await SaveOrUpdateAsync(loaded);

        Timesheet reloaded = (await Repository().FindAsync(_eve, ThisWeek, Ct))!;
        reloaded.Version.Should().Be(2);
        reloaded.Entries.Single().End.Should().Be(CompanyClock.At("2026-10-30 12:00"));
        reloaded.Entries.Single().ClockOutSource.Should().Be(Laptop);
    }

    [Fact]
    public async Task Rejects_a_save_when_someone_else_saved_first()
    {
        var timesheet = Timesheet.Start(_eve, ThisWeek);
        timesheet.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);
        await SaveOrUpdateAsync(timesheet);
        Timesheet first = (await Repository().FindAsync(_eve, ThisWeek, Ct))!;
        Timesheet second = (await Repository().FindAsync(_eve, ThisWeek, Ct))!;
        first.ClockOut(CompanyClock.At("2026-10-30 12:00"), Laptop);
        second.ClockOut(CompanyClock.At("2026-10-30 12:01"), Phone);
        await SaveOrUpdateAsync(first);

        var act = () => SaveOrUpdateAsync(second);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task Rejects_a_second_timesheet_when_the_week_already_has_one()
    {
        await SaveOrUpdateAsync(Timesheet.Start(_eve, ThisWeek));

        var act = () => SaveOrUpdateAsync(Timesheet.Start(_eve, ThisWeek));

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("ConcurrencyConflict");
    }

    [Fact]
    public async Task Rejects_a_second_open_entry_when_it_is_in_another_week()
    {
        var lastWeek = Timesheet.Start(_eve, LastWeek);
        lastWeek.ClockIn(CompanyClock.At("2026-10-25 22:00"), Phone, CompanyClock.Zone);
        await SaveOrUpdateAsync(lastWeek);
        var thisWeek = Timesheet.Start(_eve, ThisWeek);
        thisWeek.ClockIn(CompanyClock.At("2026-10-30 08:00"), Phone, CompanyClock.Zone);

        // As a command runs it: one scope, inside the unit of work.
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IUnitOfWork unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginAsync(Ct);

        var act = () => scope.ServiceProvider.GetRequiredService<ITimesheetRepository>().SaveOrUpdateAsync(thisWeek, Ct);

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("EntryAlreadyOpen");
        await unitOfWork.DisposeAsync();
        (await Repository().FindAsync(_eve, ThisWeek, Ct)).Should().BeNull("the failed save is rolled back");
    }

    public void Dispose() => _scopes.Dispose();

    private ITimesheetRepository Repository() => _scopes.Get<ITimesheetRepository>();

    private Task SaveOrUpdateAsync(Timesheet timesheet) => Repository().SaveOrUpdateAsync(timesheet, Ct);
}
