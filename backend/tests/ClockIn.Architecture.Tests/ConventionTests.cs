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

    [Theory]
    [MemberData(nameof(Layers))]
    public void Repository_parameters_and_fields_end_with_Repository(string layer)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        static bool IsRepository(Type type) => type.Name.EndsWith("Repository", StringComparison.Ordinal);

        // Compiler-generated members (primary-constructor captures, async state machines) carry '<' in their names.
        var offenders = Assembly.Load(layer).GetTypes()
            .Where(t => t.Namespace?.StartsWith("ClockIn", StringComparison.Ordinal) == true)
            .SelectMany(t =>
                t.GetConstructors(declared).Cast<MethodBase>().Concat(t.GetMethods(declared))
                    .SelectMany(m => m.GetParameters())
                    .Where(p => IsRepository(p.ParameterType))
                    .Select(p => (Type: t, Name: p.Name ?? ""))
                    .Concat(t.GetFields(declared)
                        .Where(f => IsRepository(f.FieldType))
                        .Select(f => (Type: t, f.Name))))
            .Where(x => !x.Name.Contains('<') && !x.Name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Type.FullName}.{x.Name}")
            .Distinct();

        string.Join(", ", offenders).Should().BeEmpty();
    }
}
