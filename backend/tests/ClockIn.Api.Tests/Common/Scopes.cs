using Microsoft.Extensions.DependencyInjection;

namespace ClockIn.Api.Tests.Common;

/// <summary>Resolves each service from a fresh scope, like separate requests, and disposes them all at the end.</summary>
public sealed class Scopes(ApiFactory factory) : IDisposable
{
    private readonly List<IServiceScope> _scopes = [];

    public T Get<T>()
        where T : notnull
    {
        IServiceScope scope = factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public void Dispose() => _scopes.ForEach(scope => scope.Dispose());
}
