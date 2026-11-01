using AwesomeAssertions;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Accounts;
using ClockIn.Domain.Timesheets;
using ClockIn.Persistence.Accounts;
using ClockIn.Persistence.Common;
using ClockIn.Persistence.Reporting;
using ClockIn.Persistence.Teams;
using ClockIn.Persistence.Timesheets;
using LinqToDB;

namespace ClockIn.Api.Tests.Reporting;

/// <summary>The same reporting SQL must also run on the Development engine.</summary>
public sealed class SqliteReportingReadsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9_999_999)]
    public async Task Aggregates_and_pages_on_sqlite(int extraTicks)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using var db = new AppDataConnection(new DataOptions().UseConnectionString(ProviderName.SQLiteMS, "Data Source=:memory:"));
        await db.CreateTableAsync<UserRecord>(token: ct);
        await db.CreateTableAsync<TeamRecord>(token: ct);
        await db.CreateTableAsync<TimesheetRecord>(token: ct);
        await db.CreateTableAsync<TimeEntryRecord>(token: ct);
        await db.CreateTableAsync<TimesheetHistoryRecord>(token: ct);
        await db.CreateTableAsync<CorrectionRequestRecord>(token: ct);
        var owner = User.Create(new Email("eve@clockin.test"), "Eve", Role.Employee);
        await new UserRepository(db).SaveOrUpdateAsync(owner, ct);
        var week = new Week(new DateOnly(2026, 10, 19));
        var start = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.FromHours(-5));
        DateTimeOffset now = start.AddDays(10);
        var sheet = Timesheet.Start(owner.Id, week);
        sheet.AddEntry(owner.Id, start, start.AddHours(11).AddTicks(extraTicks), "long shift", new PunchSource("127.0.0.1", "test"), now, TimeZoneInfo.Utc, []);
        sheet.Submit(owner.Id, now, TimeZoneInfo.Utc);
        await new TimesheetRepository(db).SaveOrUpdateAsync(sheet, ct);
        var request = CorrectionRequest.Create(sheet.Id, owner.Id, "Missing shift", now);
        await new CorrectionRequestRepository(db, new PendingDomainEvents()).SaveOrUpdateAsync(request, ct);
        var reads = new ReportingReads(db);

        ReviewQueueDto queue = await reads.ReviewQueueAsync([owner.Id.Value], 1, 1, now, ct);
        ReportPageDto<ExceptionDto> exceptions = await reads.ExceptionsAsync([owner.Id.Value], week.Start.AddDays(14), 1, ct);

        queue.Timesheets.Should().ContainSingle().Which.TotalSeconds.Should().Be(11 * 3600);
        queue.Corrections.Should().ContainSingle();
        exceptions.Items.Should().ContainSingle().Which.Seconds.Should().Be(11 * 3600);
        (await reads.ReviewCountsAsync([owner.Id.Value], ct)).Should().Be((1, 1));
        (await reads.HasPendingReviewsAsync([owner.Id.Value], ct)).Should().BeTrue();
        (await reads.PersonalToDosAsync(owner.Id.Value, week.Start.AddDays(14), now.AddDays(-7), ct)).UnsubmittedWeeks.Should().BeEmpty();
        (await reads.ClockedInAsync([owner.Id.Value], now, ct)).Should().BeEmpty();
    }
}
