using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpFunctionalExtensions.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferImplicitCastingOfResultCodeFixProvider)), Shared]
public class PreferImplicitCastingOfResultCodeFixProvider : CodeFixProvider
{
    public const string DiagnosticId = PreferImplicitCastingOfResult.DiagnosticId;
    private const string Title = "Use implicit type argument";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the type argument syntax within the invocation
        var typeArgumentSyntax = root.FindToken(diagnosticSpan.Start)
            .Parent.AncestorsAndSelf()
            .OfType<TypeArgumentListSyntax>()
            .First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: c => RemoveExplicitTypeArgument(context.Document, typeArgumentSyntax, c),
                equivalenceKey: Title
            ),
            diagnostic
        );
    }

    private async Task<Document> RemoveExplicitTypeArgument(
        Document document,
        TypeArgumentListSyntax typeArgumentSyntax,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        // Generate a new invocation without the type argument
        var newInvocation = typeArgumentSyntax.Parent.ReplaceNode(
            typeArgumentSyntax,
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>())
        );

        // Replace the old invocation with the new one
        var newRoot = root.ReplaceNode(typeArgumentSyntax.Parent, newInvocation);

        return document.WithSyntaxRoot(newRoot);
    }
}
