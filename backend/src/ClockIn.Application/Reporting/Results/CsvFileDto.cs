namespace ClockIn.Application.Reporting.Results;

public sealed record CsvFileDto(string FileName, byte[] Content);
