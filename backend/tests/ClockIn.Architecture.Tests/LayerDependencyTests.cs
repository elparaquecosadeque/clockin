using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace ClockIn.Architecture.Tests;

/// <summary>Dependencies point inward (CODE_STYLE.md, Project dependencies).</summary>
public sealed class LayerDependencyTests
{
    private const string Domain = "ClockIn.Domain";
    private const string Application = "ClockIn.Application";
    private const string Persistence = "ClockIn.Persistence";
    private const string Infrastructure = "ClockIn.Infrastructure";
    private const string Api = "ClockIn.Api";
    private const string PublicApi = "ClockIn.PublicApi";
    private const string SecondPublicApi = "ClockIn.SecondPublicApi";
    private const string Hosting = "ClockIn.Hosting";

    [Fact]
    public void Domain_depends_on_no_other_layer() =>
        ShouldNotDependOn(Domain, Application, Persistence, Infrastructure, Api, PublicApi, SecondPublicApi, Hosting);

    [Fact]
    public void Application_depends_only_on_domain() =>
        ShouldNotDependOn(Application, Persistence, Infrastructure, Api, PublicApi, SecondPublicApi, Hosting);

    [Fact]
    public void Persistence_does_not_depend_on_infrastructure_or_api() =>
        ShouldNotDependOn(Persistence, Infrastructure, Api, PublicApi, SecondPublicApi, Hosting);

    [Fact]
    public void Infrastructure_does_not_depend_on_persistence_or_api() =>
        ShouldNotDependOn(Infrastructure, Persistence, Api, PublicApi, SecondPublicApi, Hosting);

    [Theory]
    [InlineData(Api)]
    [InlineData(PublicApi)]
    [InlineData(SecondPublicApi)]
    public void Controllers_use_neither_persistence_nor_infrastructure(string host)
    {
        var result = Types.InAssembly(Assembly.Load(host))
            .That().ResideInNamespace($"{host}.Controllers")
            .ShouldNot().HaveDependencyOnAny(Persistence, Infrastructure)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData(PublicApi, SecondPublicApi)]
    [InlineData(SecondPublicApi, PublicApi)]
    public void Public_host_has_no_mediator_or_other_host_dependency(string host, string otherHost) =>
        ShouldNotDependOn(host, Api, otherHost, "Wolverine");

    [Fact]
    public void Shared_host_helpers_depend_on_neither_host_nor_persistence()
    {
        // NetArchTest treats the SPA JWT audience string as a type dependency; check real assembly references here.
        string[] forbidden = [Api, PublicApi, SecondPublicApi, Persistence, Infrastructure];
        Assembly.Load(Hosting).GetReferencedAssemblies().Select(a => a.Name)
            .Should().NotContain(name => forbidden.Contains(name) || name!.StartsWith("Wolverine", StringComparison.Ordinal));
    }

    private static void ShouldNotDependOn(string layer, params string[] forbidden)
    {
        var result = Types.InAssembly(Assembly.Load(layer))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
