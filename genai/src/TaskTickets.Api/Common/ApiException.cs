namespace TaskTickets.Api.Common;

public sealed class ApiException(int statusCode, string message, string? field = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? Field { get; } = field;
}
