namespace ClockIn.Api.Tests.Common;

/// <summary>Shares one API host and one database container across the test classes.</summary>
[CollectionDefinition(Name)]
public sealed class SharedApi : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
