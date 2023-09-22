using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;

namespace CSharpFunctionalExtensions.Analyzers.Tests;

public sealed class DummyCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => throw new NotSupportedException();

    public override Task RegisterCodeFixesAsync(CodeFixContext context) => throw new NotSupportedException();

    public override FixAllProvider GetFixAllProvider() => throw new NotSupportedException();
}
