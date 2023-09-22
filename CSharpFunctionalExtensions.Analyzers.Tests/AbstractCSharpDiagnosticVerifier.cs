using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslynator.Testing;
using Roslynator.Testing.CSharp;
using Roslynator.Testing.CSharp.Xunit;

namespace CSharpFunctionalExtensions.Analyzers.Tests;

//Copied from https://github.com/JosefPihrt/Roslynator/blob/61265c48bb120f5e9fbf3ec9bc90974bd325c0e2/src/Tests/Tests.Common/Testing/CSharp/AbstractCSharpDiagnosticVerifier.cs#L15
public abstract class AbstractCSharpDiagnosticVerifier<TAnalyzer, TFixProvider> : XunitDiagnosticVerifier<TAnalyzer, TFixProvider>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFixProvider : CodeFixProvider, new()
{
    public abstract DiagnosticDescriptor Descriptor { get; }

    public override CSharpTestOptions Options => CSharpTestOptions.Default;

    public async Task VerifyDiagnosticAsync(
        string source,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var code = TestCode.Parse(source);

        var data = new DiagnosticTestData(
            Descriptor,
            code.Value,
            code.Spans,
            code.AdditionalSpans
        );

        await VerifyDiagnosticAsync(
            data,
            options: options,
            cancellationToken: cancellationToken);
    }

    public async Task VerifyDiagnosticAsync(
        string source,
        string sourceData,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var code = TestCode.Parse(source, sourceData);

        var data = new DiagnosticTestData(
            Descriptor,
            source,
            code.Spans,
            code.AdditionalSpans);

        await VerifyDiagnosticAsync(
            data,
            options: options,
            cancellationToken: cancellationToken);
    }

    internal async Task VerifyDiagnosticAsync(
        string source,
        TextSpan span,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var data = new DiagnosticTestData(
            Descriptor,
            source,
            ImmutableArray.Create(span));

        await VerifyDiagnosticAsync(
            data,
            options: options,
            cancellationToken: cancellationToken);
    }

    internal async Task VerifyDiagnosticAsync(
        string source,
        IEnumerable<TextSpan> spans,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var data = new DiagnosticTestData(
            Descriptor,
            source,
            spans
        );

        await VerifyDiagnosticAsync(
            data,
            options: options,
            cancellationToken: cancellationToken);
    }

    public async Task VerifyNoDiagnosticAsync(
        string source,
        string sourceData,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var code = TestCode.Parse(source, sourceData);

        var data = new DiagnosticTestData(
            Descriptor,
            code.Value,
            spans: null,
            code.AdditionalSpans);

        await VerifyNoDiagnosticAsync(
            data,
            options: options,
            cancellationToken);
    }

    public async Task VerifyNoDiagnosticAsync(
        string source,
        IEnumerable<string> additionalFiles = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var data = new DiagnosticTestData(
            Descriptor,
            source,
            spans: null);

        await VerifyNoDiagnosticAsync(
            data,
            options: options,
            cancellationToken);
    }


    public async Task VerifyDiagnosticAndNoFixAsync(
        string source,
        IEnumerable<(string source, string expectedSource)> additionalFiles = null,
        string equivalenceKey = null,
        TestOptions options = null,
        CancellationToken cancellationToken = default)
    {
        var code = TestCode.Parse(source);

        var data = new DiagnosticTestData(
            Descriptor,
            code.Value,
            code.Spans,
            additionalSpans: code.AdditionalSpans,
            equivalenceKey: equivalenceKey);

        await VerifyDiagnosticAndNoFixAsync(data, options, cancellationToken);
    }
}

