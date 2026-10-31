using System.Text;
using AwesomeAssertions;
using ClockIn.Application.Common.Exceptions;
using ClockIn.Application.Reporting;
using ClockIn.Application.Reporting.Queries;
using ClockIn.Application.Reporting.Results;
using ClockIn.Domain.Timesheets;
using ClockIn.Tests.Common;

namespace ClockIn.Application.Tests.Reporting;

/// <summary>export-approved-csv.feature.</summary>
public sealed class ExportApprovedCsvTests
{
    private readonly ReportingWorld _world = new();

    [Fact]
    public async Task Exports_the_teams_approved_entries_as_utf8_with_bom_and_crlf()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Approved,
            [("2026-10-20 08:00", "2026-10-20 16:45"), ("2026-10-21 08:00", "2026-10-21 12:00")], approvedTeam: _world.Warehouse.Id);
        _world.Timesheet(_world.Ed, ReportingWorld.LastWeek, TimesheetStatus.Submitted, [("2026-10-20 08:00", "2026-10-20 12:00")]);

        CsvFileDto file = await Export(ReportingWorld.LastWeek);

        file.Content.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        string[] lines = Text(file).Split("\r\n");
        lines[0].Should().Be(ApprovedHoursCsv.Header);
        lines[1].Should().Be("Eve,eve@clockin.test,Warehouse,2026-10-19,2026-10-20,08:00,16:45,8:45,8.75,false,");
        lines.Should().HaveCount(4, "two rows and the final line break");
        file.FileName.Should().Be("clockin-approved-2026-10-19.csv");
    }

    [Theory]
    [InlineData(7 * 3600 + 45 * 60, "7:45", "7.75")]
    [InlineData(7 * 3600 + 44 * 60 + 30, "7:45", "7.74")]
    [InlineData(29, "0:00", "0.01")]
    [InlineData(16 * 3600, "16:00", "16.00")]
    public void Formats_hours_both_ways_from_the_exact_seconds(int seconds, string hours, string hoursDecimal)
    {
        DateTimeOffset start = CompanyClock.At("2026-10-20 08:00");

        string row = Text(ApprovedHoursCsv.Write(
            [new ApprovedHoursLine("Eve", "eve@x", null, ReportingWorld.LastWeek, start, start.AddSeconds(seconds), false, null)],
            CompanyClock.Zone)).Split("\r\n")[1];

        row.Split(',')[7..9].Should().Equal(hours, hoursDecimal);
    }

    [Theory]
    [InlineData("client, on site", "\"client, on site\"")]
    [InlineData("said \"hi\"", "\"said \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+1 hour", "'+1 hour")]
    [InlineData("-1 hour", "'-1 hour")]
    [InlineData("@sum", "'@sum")]
    public void Escapes_text_safely(string note, string cell) => ApprovedHoursCsv.Cell(note).Should().Be(cell);

    [Fact]
    public async Task Produces_only_a_header_when_nothing_is_approved()
    {
        Text(await Export(new DateOnly(2026, 10, 12))).Should().Be(ApprovedHoursCsv.Header + "\r\n");
    }

    [Fact]
    public async Task Keeps_the_team_the_timesheet_had_when_approved()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Approved, [("2026-10-20 08:00", "2026-10-20 12:00")],
            approvedTeam: _world.Warehouse.Id);
        _world.Move(_world.Eve, _world.Office.Id);

        string sams = Text(await Export(ReportingWorld.LastWeek));
        _world.SignIn(_world.Ada);
        string adas = Text(await Export(ReportingWorld.LastWeek));

        sams.Should().Contain("Eve,eve@clockin.test,Warehouse,");
        adas.Should().Contain("Eve,eve@clockin.test,Warehouse,");
    }

    [Fact]
    public async Task Lets_admins_export_everyone_with_an_empty_team_for_supervisors_or_filter_by_team()
    {
        _world.Timesheet(_world.Eve, ReportingWorld.LastWeek, TimesheetStatus.Approved, [("2026-10-20 08:00", "2026-10-20 12:00")],
            approvedTeam: _world.Warehouse.Id);
        _world.Timesheet(_world.Sam, ReportingWorld.LastWeek, TimesheetStatus.Approved, [("2026-10-20 08:00", "2026-10-20 12:00")]);
        _world.SignIn(_world.Ada);

        string all = Text(await Export(ReportingWorld.LastWeek));
        string office = Text(await Export(ReportingWorld.LastWeek, _world.Office.Id));

        all.Should().Contain("Sam,sam@clockin.test,,2026-10-19").And.Contain("Eve,");
        office.Should().Be(ApprovedHoursCsv.Header + "\r\n");
    }

    [Fact]
    public async Task Forbids_exporting_outside_ones_scope()
    {
        var otherTeam = () => Export(ReportingWorld.LastWeek, _world.Office.Id);
        await otherTeam.Should().ThrowAsync<ForbiddenException>();

        _world.SignIn(_world.Eve);
        var employee = () => Export(ReportingWorld.LastWeek, _world.Warehouse.Id);
        await employee.Should().ThrowAsync<ForbiddenException>();
    }

    private static string Text(CsvFileDto file) => Text(file.Content);

    private static string Text(byte[] content) => new UTF8Encoding(false).GetString(content).TrimStart('﻿');

    private Task<CsvFileDto> Export(DateOnly week, Guid? team = null) =>
        new ExportApprovedCsvHandler(_world.CurrentUser, _world.Reads, _world.Clock)
            .HandleAsync(new ExportApprovedCsvQuery(week, team), CancellationToken.None);
}
