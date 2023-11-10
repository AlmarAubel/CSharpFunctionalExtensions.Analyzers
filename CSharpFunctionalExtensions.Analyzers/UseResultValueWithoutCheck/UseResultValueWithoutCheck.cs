using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq.Expressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharpFunctionalExtensions.Analyzers.UseResultValueWithoutCheck;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseResultValueWithoutCheck : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CFE0001";
    private const string Title = "Check IsSuccess or IsFailure before accessing Value from result object";
    private const string MessageFormat = "Accessing Value without checking IsSuccess or IsFailure can result in an unexpected Errors";
    private const string Category = "Usage";

    private const string HelpLinkUri =
        "https://github.com/vkhorikov/CSharpFunctionalExtensions#get-rid-of-primitive-obsession";

    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkUri
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.SimpleMemberAccessExpression);
    }

    private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (memberAccess.Name.ToString() != "Value")
            return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess);

        if (
            symbolInfo.Symbol is not IPropertySymbol { ContainingType.Name: "Result" } memberSymbol
            || memberSymbol.ContainingType.ContainingNamespace.ToString() != "CSharpFunctionalExtensions"
        )
        {
            return;
        }

        var checksSucces = TraversFunctionBody(memberAccess);

        //Check if accessed inside if statement
        //var checksSucces = HasBeenCheckedBeforeAccess(memberAccess);
        if (checksSucces)
            return;

        var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.Expression);
        context.ReportDiagnostic(diagnostic);
    }

    private bool TraversFunctionBody(MemberAccessExpressionSyntax memberAccess)
    {
        // Find the enclosing method block
        if (memberAccess.Ancestors().FirstOrDefault(a => a is MethodDeclarationSyntax) is not MethodDeclarationSyntax methodDeclaration)
            return true;

        if (methodDeclaration.Body is null) return true;
        var walker = new ResultValueWalker(memberAccess);
        var result = walker.NodeWalker(methodDeclaration.Body);
        return result.CorrectUsage;

    }
}
