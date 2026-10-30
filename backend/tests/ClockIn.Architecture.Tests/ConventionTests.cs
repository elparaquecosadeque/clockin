using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace ClockIn.Architecture.Tests;

public sealed class ConventionTests
{
    public static TheoryData<string> Layers =>
        ["ClockIn.Domain", "ClockIn.Application", "ClockIn.Persistence", "ClockIn.Infrastructure", "ClockIn.Api"];

    [Theory]
    [MemberData(nameof(Layers))]
    public void Classes_are_sealed_unless_abstract_or_static(string layer)
    {
        // Our own namespaces only: compilers and source generators add types of their own.
        var result = Types.InAssembly(Assembly.Load(layer))
            .That().ResideInNamespaceStartingWith("ClockIn")
            .And().AreClasses().And().AreNotAbstract().And().AreNotStatic()
            .Should().BeSealed()
            .GetResult();

        result.FailingTypeNames.Should().BeNullOrEmpty();
    }
}
