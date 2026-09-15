using System.Runtime.CompilerServices;
using Norn.Contracts;
using Shouldly;
using Xunit;

namespace Norn.ArchitectureTests;

/// <summary>
/// Norn.Contracts é o núcleo (ADR-17): nenhum PackageReference nele, nunca — se um pacote
/// precisar entrar, a abstração está vazando o adaptador.
/// </summary>
public sealed class NornContractsPurityTests
{
    [Fact]
    public void NornContractsCsproj_Should_NotDeclare_AnyPackageOrProjectReference()
    {
        var csprojPath = Path.Combine(GetRepoRoot(), "src", "Platform", "Norn.Contracts", "Norn.Contracts.csproj");
        var csprojContent = File.ReadAllText(csprojPath);

        csprojContent.ShouldNotContain("<PackageReference");
        csprojContent.ShouldNotContain("<ProjectReference");
    }

    /// <summary>
    /// Defesa em profundidade: mesmo que uma referência escape da checagem acima (p.ex. via
    /// Directory.Build.props), o tipo só vaza de verdade se algo em Norn.Contracts o usar — e é
    /// isso que aparece na assembly compilada. Uma referência declarada e não usada não aparece
    /// aqui, porque o compilador não emite metadado para ela; por isso o teste acima é o principal.
    /// </summary>
    [Fact]
    public void NornContracts_Should_NotReferenceAnyAssembly_OutsideTheRuntime()
    {
        var assembly = typeof(AnomalyContext).Assembly;

        var externalReferences = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => !IsRuntimeAssembly(name))
            .ToArray();

        externalReferences.ShouldBeEmpty();
    }

    private static bool IsRuntimeAssembly(string? assemblyName) =>
        assemblyName is not null &&
        (assemblyName.StartsWith("System", StringComparison.Ordinal) || assemblyName is "netstandard" or "mscorlib");

    private static string GetRepoRoot([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", ".."));
}
