using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CSharpFunctionalExtensions.Analyzers.UseResultValueWithoutCheck;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseResultValueWithoutCheck : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CFE0001";
    private const string Title = "Check IsSuccess or IsError before accessing Value from result object";
    private const string MessageFormat = "Accessing Value without checking IsSuccess or IsError can result in an error";
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

        // Get the enclosing block (e.g., the method or property body)
        var checks = TerminatedBeforeAccessWhenNotSuccess(memberAccess);
        if (checks.Any())
            return;

        //Check if accessed inside if statement
        var checksSucces = HasBeenCheckedBeforeAccess(memberAccess);
        if (checksSucces.Any())
            return;

        var diagnostic = Diagnostic.Create(Rule, memberAccess.GetLocation(), memberAccess.Expression);
        context.ReportDiagnostic(diagnostic);
    }

    private static IEnumerable<SyntaxNode> HasBeenCheckedBeforeAccess(MemberAccessExpressionSyntax memberAccess)
    {
        var enclosingControlStructures = memberAccess
            .AncestorsAndSelf()
            .Where(a => a is IfStatementSyntax or ConditionalExpressionSyntax);

        var checksSucces = enclosingControlStructures
            .Where(
                structure =>
                    (
                        structure is IfStatementSyntax ifStatement
                        && DetermineCheckResult(ifStatement.Condition) == CheckResult.CheckedSuccess
                    )
                    || (
                        structure is ConditionalExpressionSyntax ternary
                        && DetermineCheckResult(ternary.Condition) == CheckResult.CheckedSuccess
                    )
            )
            .ToList();

        return checksSucces;
    }

    private IEnumerable<IfStatementSyntax> TerminatedBeforeAccessWhenNotSuccess(SyntaxNode memberAccess)
    {
        var enclosingBlock = memberAccess.AncestorsAndSelf().FirstOrDefault(a => a is BlockSyntax);
        if (enclosingBlock == null)
            return Enumerable.Empty<IfStatementSyntax>();

        // Check if the Value variable is checked is succes and returned before accessing it
        return enclosingBlock
            .DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(ifStatement =>
            {
                var willExecute = DetermineCheckResult(ifStatement.Condition) == CheckResult.CheckedFailure;

                if (
                    willExecute
                    && (
                        IsInside<BinaryExpressionSyntax>(ifStatement, memberAccess)
                        || IsInside<ConditionalExpressionSyntax>(ifStatement, memberAccess)
                    )
                )
                    return true;

                return willExecute && ContainsTerminatingStatement(ifStatement.Statement) && !ContainsSyntaxNode(ifStatement.Statement, memberAccess);
            })
            .ToList();
    }
    private static bool IsInside<T>(SyntaxNode statement, SyntaxNode memberAccess)
        where T : SyntaxNode
    {
        return statement
            .DescendantNodes()
            .OfType<T>()
            .Any(expression => expression.DescendantNodesAndSelf().Any(a => memberAccess == a));
    }

    private static CheckResult DetermineCheckResult(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binaryExpression:
                switch (binaryExpression.OperatorToken.Kind())
                {
                    case SyntaxKind.AmpersandAmpersandToken:
                        {
                            var leftResult = DetermineCheckResult(binaryExpression.Left);
                            var rightResult = DetermineCheckResult(binaryExpression.Right);
                            if (leftResult == CheckResult.Unchecked)
                                return rightResult;
                            if (rightResult == CheckResult.Unchecked)
                                return leftResult;
                            // If both sides are the same, return either; otherwise, it's ambiguous so return Unchecked.
                            return leftResult == rightResult ? leftResult : CheckResult.Unchecked;
                        }
                    case SyntaxKind.BarBarToken:
                        {
                            var leftResult = DetermineCheckResult(binaryExpression.Left);
                            var rightResult = DetermineCheckResult(binaryExpression.Right);

                            if (leftResult is CheckResult.Unchecked or CheckResult.CheckedFailure)
                                return leftResult;

                            if (rightResult == CheckResult.Unchecked)
                                return rightResult;

                            // If both sides are the same, return either; otherwise, it's ambiguous so return Unchecked.
                            return leftResult == rightResult ? leftResult : CheckResult.Unchecked;
                        }
                    case SyntaxKind.EqualsEqualsToken:
                        {
                            var leftExpression = binaryExpression.Left.ToString();
                            var rightExpression = binaryExpression.Right.ToString();

                            if (IsSuccess(leftExpression, rightExpression))
                                return CheckResult.CheckedSuccess;

                            if (IsFailure(leftExpression, rightExpression))
                                return CheckResult.CheckedFailure;

                            break;
                        }
                    case SyntaxKind.ExclamationEqualsToken:
                        {
                            var leftExpression = binaryExpression.Left.ToString();
                            var rightExpression = binaryExpression.Right.ToString();
                            if (IsFailure(leftExpression, rightExpression))
                                return CheckResult.CheckedFailure;

                            if (IsSuccess(leftExpression, rightExpression))
                                return CheckResult.CheckedSuccess;

                            break;
                        }
                }

                break;
            case MemberAccessExpressionSyntax memberAccess:
                switch (memberAccess.Name.ToString())
                {
                    case "IsSuccess":
                        return CheckResult.CheckedSuccess;
                    case "IsFailure":
                        return CheckResult.CheckedFailure;
                }

                break;
            case PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.Operand.ToString().Contains("IsSuccess"):
                return CheckResult.CheckedFailure; // This means we found a !IsSuccess, so it's equivalent to IsFailure.
            case PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.Operand.ToString().Contains("IsFailure"):
                return CheckResult.CheckedSuccess; // This means we found a !IsFailure, so it's equivalent to IsSuccess.
            case ConditionalExpressionSyntax ternary:
                return DetermineCheckResult(ternary.Condition);
        }

        return CheckResult.Unchecked;
    }

    private static bool IsSuccess(string leftExpression, string rightExpression)
    {
        return leftExpression.Contains("IsSuccess") && rightExpression == "true"
               || leftExpression.Contains("IsFailure") && rightExpression == "false";
    }

    private static bool IsFailure(string leftExpression, string rightExpression)
    {
        return leftExpression.Contains("IsSuccess") && rightExpression == "false"
               || leftExpression.Contains("IsFailure") && rightExpression == "true";
    }

    private static bool ContainsTerminatingStatement(StatementSyntax statement)
    {
        return statement switch
        {
            // Check for return or throw statements directly within the provided statement
            ReturnStatementSyntax or ThrowStatementSyntax => true,
            // If the statement is a block, check its child statements
            BlockSyntax block
                => block.Statements.Any(
                    childStatement => childStatement is ReturnStatementSyntax or ThrowStatementSyntax
                ),
            _ => false
        };
    }
    
    private static bool ContainsSyntaxNode(SyntaxNode statement, SyntaxNode targetNode)
    {
        return statement.DescendantNodesAndSelf().Any(n => n == targetNode);
    }

    private enum CheckResult
    {
        CheckedSuccess,
        CheckedFailure,
        Unchecked
    }
}
