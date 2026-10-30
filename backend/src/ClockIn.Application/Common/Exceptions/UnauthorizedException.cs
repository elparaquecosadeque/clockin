namespace ClockIn.Application.Common.Exceptions;

/// <summary>The caller's credentials or session are not valid. Never says why.</summary>
public sealed class UnauthorizedException() : Exception("Not authenticated.");
