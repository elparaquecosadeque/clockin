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

    [Fact]
    public void Domain_depends_on_no_other_layer() =>
        ShouldNotDependOn(Domain, Application, Persistence, Infrastructure, Api);

    [Fact]
    public void Application_depends_only_on_domain() =>
        ShouldNotDependOn(Application, Persistence, Infrastructure, Api);

    [Fact]
    public void Persistence_does_not_depend_on_infrastructure_or_api() =>
        ShouldNotDependOn(Persistence, Infrastructure, Api);

    [Fact]
    public void Infrastructure_does_not_depend_on_persistence_or_api() =>
        ShouldNotDependOn(Infrastructure, Persistence, Api);

    [Fact]
    public void Controllers_use_neither_persistence_nor_infrastructure()
    {
        var result = Types.InAssembly(Assembly.Load(Api))
            .That().ResideInNamespace($"{Api}.Controllers")
            .ShouldNot().HaveDependencyOnAny(Persistence, Infrastructure)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }

    private static void ShouldNotDependOn(string layer, params string[] forbidden)
    {
        var result = Types.InAssembly(Assembly.Load(layer))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
