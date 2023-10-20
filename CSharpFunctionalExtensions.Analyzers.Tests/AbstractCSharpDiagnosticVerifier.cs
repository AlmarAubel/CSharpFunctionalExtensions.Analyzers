using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace CSharpFunctionalExtensions.Analyzers.Tests;

public abstract class AbstractCSharpDiagnosticVerifier<TAnalyzer, TFixProvider>
    : XunitDiagnosticVerifier<TAnalyzer, TFixProvider>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFixProvider : CodeFixProvider, new()
{
    public override CSharpTestOptions Options => DefaultCSharpTestOptions.Value;
}

internal static class DefaultCSharpTestOptions
{
    public static CSharpTestOptions Value { get; } = Create();

    private static CSharpTestOptions Create()
    {
        ImmutableArray<string> allowedCompilerDiagnosticIds = ImmutableArray.Create(
            "CS0067", // Event is never used
            "CS0168", // Variable is declared but never used
            "CS0169", // Field is never used
            "CS0219", // Variable is assigned but its value is never used
            "CS0414", // Field is assigned but its value is never used
            "CS0649", // Field is never assigned to, and will always have its default value null
            "CS0660", // Type defines operator == or operator != but does not override Object.Equals(object o)
            "CS0661", // Type defines operator == or operator != but does not override Object.GetHashCode()
            "CS8019", // Unnecessary using directive
            "CS8321" // The local function is declared but never used
        );

        return CSharpTestOptions.Default
            .WithParseOptions(CSharpTestOptions.Default.ParseOptions.WithLanguageVersion(LanguageVersion.CSharp10))
            .WithAllowedCompilerDiagnosticIds(allowedCompilerDiagnosticIds)
            .AddConfigOptions(("indent_size", "4"), ("indent_style", "space"));
    }
}
