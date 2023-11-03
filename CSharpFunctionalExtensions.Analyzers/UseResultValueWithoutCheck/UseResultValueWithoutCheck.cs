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

        return NodeWalker(memberAccess, methodDeclaration.Body).CorrectUsage;
    }


    private static WalkerResult NodeWalker(MemberAccessExpressionSyntax memberAccessValueResult, SyntaxNode node, WalkerResult? result = null)
    {
        result ??= WalkerResult.Default;

        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                case IfStatementSyntax ifStatement:
                    {
                        result.CheckResult = DetermineCheckResult(ifStatement.Condition);
                        var wresult = NodeWalker(memberAccessValueResult, ifStatement, result);
                        if (wresult.CorrectUsage) return wresult;
                        if (wresult is { CheckResult: CheckResult.CheckedSuccess,Terminated:true, AccessedValue: false })
                        {
                            wresult.CheckResult = CheckResult.Unchecked;
                        }
                        
                        wresult.Terminated = false;
                        break;
                    }
                case ReturnStatementSyntax or ThrowStatementSyntax:
                    result.Terminated = true;
                    return NodeWalker(memberAccessValueResult, child, result);
                case ConditionalExpressionSyntax ternary:
                    CheckTernaryCondition(memberAccessValueResult, result, ternary);

                    if (!result.CorrectUsage) return result;
                    break;
                case SwitchExpressionArmSyntax switchExpressionArmSyntax:
                    result.CheckResult = CheckSwitchExpressionArm(switchExpressionArmSyntax);
                    if (result.CorrectUsage) return result;
                    break;
                case MemberAccessExpressionSyntax ma:
                    if (memberAccessValueResult == ma)
                    {
                        result.AccessedValue = true;
                        return result;
                    }

                    break;
            }

            // Recursively analyze child nodes
            var r = NodeWalker(memberAccessValueResult, child, result);
            if (r.CorrectUsage) return r;
        }

        return result;
    }

    private static void CheckTernaryCondition(MemberAccessExpressionSyntax memberAccessValueResult, WalkerResult result,
        ConditionalExpressionSyntax ternary)
    {
        result.CheckResult = DetermineCheckResult(ternary.Condition);
        switch (result.CheckResult)
        {
            case CheckResult.CheckedSuccess:
                result.AccessedValue = ternary.WhenTrue == memberAccessValueResult && ternary.WhenFalse != memberAccessValueResult;
                break;
            case CheckResult.CheckedFailure:
                result.AccessedValue = ternary.WhenFalse == memberAccessValueResult && ternary.WhenTrue != memberAccessValueResult;
                break;
        }
    }


    private static CheckResult DetermineCheckResult(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binaryExpression:
                return BinaryExpressionSyntax(binaryExpression);
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
            case SwitchExpressionSyntax switchExpressionSyntax:
                throw new NotImplementedException();
        }

        return CheckResult.Unchecked;
    }

    private static CheckResult BinaryExpressionSyntax(BinaryExpressionSyntax binaryExpression)
    {
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
            default:
                return CheckResult.Unchecked;
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

    private static CheckResult CheckSwitchExpressionArm(SwitchExpressionArmSyntax switchExpressionArm)
    {
        var pattern = switchExpressionArm.Pattern;
        // Todo check if switchExpression is on the result object
        if (pattern is not RecursivePatternSyntax recursivePattern) return CheckResult.Unchecked;
        foreach (var propPattern in recursivePattern.PropertyPatternClause?.Subpatterns)
        {
            var name = (propPattern.Pattern as ConstantPatternSyntax)?.Expression.ToString();

            if ((name == "true" && propPattern.NameColon?.Name.Identifier.Text == "IsSuccess") ||
                (name == "false" && propPattern.NameColon?.Name.Identifier.Text == "IsFailure"))
            {
                return CheckResult.CheckedSuccess;
            }
        }

        return CheckResult.Unchecked;
    }

    private static bool ContainsSyntaxNode(SyntaxNode statement, SyntaxNode targetNode)
    {
        return statement.DescendantNodesAndSelf().Any(n => n == targetNode);
    }
}

class WalkerResult
{
    public bool AccessedValue { get; set; }
    public CheckResult CheckResult { get; set; } = CheckResult.Unchecked;
    public bool Terminated { get; set; }

    public bool CorrectUsage =>
        (AccessedValue && CheckResult == CheckResult.CheckedSuccess) || 
        (CheckResult == CheckResult.CheckedFailure && Terminated && !AccessedValue);

    public void Reset()
    {
        AccessedValue = false;
        CheckResult = CheckResult.Unchecked;
        Terminated = false;
    }

    public static WalkerResult Default => new WalkerResult();
}

enum CheckResult
{
    CheckedSuccess,
    CheckedFailure,
    Unchecked
}
