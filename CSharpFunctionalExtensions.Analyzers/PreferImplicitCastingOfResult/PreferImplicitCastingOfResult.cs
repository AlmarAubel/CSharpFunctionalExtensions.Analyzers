using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharpFunctionalExtensions.Analyzers.PreferImplicitCastingOfResult;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PreferImplicitCastingOfResult : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CFE0002";
    private const string Title = "Prefer Implicit Type Arguments for Result Methods";
    private const string MessageFormat =
        "Consider using implicit type casting instead of returning the specific type directly";
    private const string Category = "CodeStyle";
    private const string HelpLinkUri = "https://github.com/vkhorikov/CSharpFunctionalExtensions";

    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        helpLinkUri: HelpLinkUri
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (
            methodSymbol?.Name is not ("Success" or "Failure")
            || methodSymbol.ContainingType.Name != "Result"
            || methodSymbol.TypeArguments.Length <= 0
        )
        {
            return;
        }

        ITypeSymbol? expectedType = null;

        // Check if it's a return statement
        if (invocation.Parent is ReturnStatementSyntax)
        {
            var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (containingMethod != null)
            {
                var containingMethodSymbol = context.SemanticModel.GetDeclaredSymbol(containingMethod);
                expectedType = containingMethodSymbol!.ReturnType;
            }
        }
        // Check if it's an assignment
        else if (
            invocation.Parent is EqualsValueClauseSyntax { Parent.Parent: VariableDeclarationSyntax variableDeclaration })
        {
            expectedType = context.SemanticModel.GetTypeInfo(variableDeclaration.Type).Type!;
        }

        if (expectedType == null || !SymbolEqualityComparer.Default.Equals(expectedType, methodSymbol.ReturnType))
            return;

        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }
}
