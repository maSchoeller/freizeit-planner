using ArchUnitNET.Loader;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using LoadedArchitecture = ArchUnitNET.Domain.Architecture;
using ReflectionAssembly = System.Reflection.Assembly;

namespace Architecture.Tests;

public sealed class ModuleBoundaryTests
{
    private static readonly ReflectionAssembly[] ImplementationAssemblies =
    [
        typeof(Activity.Implementation.ActivityService).Assembly,
        typeof(Camps.Implementation.CampPlanningService).Assembly,
        typeof(Catering.Implementation.CateringService).Assembly,
        typeof(Files.Implementation.AttachmentService).Assembly,
        typeof(Identity.Implementation.PasswordlessLoginService).Assembly,
        typeof(Knowledge.Implementation.KnowledgeService).Assembly,
        typeof(Logistics.Implementation.LogisticsPlanningService).Assembly,
        typeof(Spiritual.Implementation.DevotionPlanningService).Assembly
    ];

    private static readonly LoadedArchitecture LoadedModules = new ArchLoader()
        .LoadAssemblies(ImplementationAssemblies)
        .Build();

    [Fact]
    public void ImplementationsDoNotCrossModuleBoundaries()
    {
        foreach (var sourceAssembly in ImplementationAssemblies)
        {
            var sourceNamespace = sourceAssembly.GetName().Name!;
            var source = Types().That().ResideInNamespace(sourceNamespace);
            foreach (var targetAssembly in ImplementationAssemblies.Where(item => item != sourceAssembly))
            {
                var targetNamespace = targetAssembly.GetName().Name!;
                var target = Types().That().ResideInNamespace(targetNamespace);
                var rule = Types().That().Are(source).Should().NotDependOnAny(target)
                    .Because("module implementations may collaborate only through the target module's Contracts assembly");

                Assert.True(
                    rule.HasNoViolations(LoadedModules),
                    $"{sourceNamespace} depends on {targetNamespace}.");
            }
        }
    }

    [Fact]
    public void ContractsDoNotReferenceImplementations()
    {
        foreach (var implementationAssembly in ImplementationAssemblies)
        {
            var contractReference = implementationAssembly.GetReferencedAssemblies()
                .Single(item => item.Name == implementationAssembly.GetName().Name!.Replace(".Implementation", ".Contracts", StringComparison.Ordinal));
            var contractAssembly = ReflectionAssembly.Load(contractReference);

            Assert.DoesNotContain(
                contractAssembly.GetReferencedAssemblies(),
                reference => reference.Name?.EndsWith(".Implementation", StringComparison.Ordinal) == true);
        }
    }
}
