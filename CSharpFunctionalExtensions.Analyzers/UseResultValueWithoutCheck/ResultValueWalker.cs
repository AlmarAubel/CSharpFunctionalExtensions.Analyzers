using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpFunctionalExtensions.Analyzers.UseResultValueWithoutCheck;

internal class ResultValueWalker
{
    private readonly MemberAccessExpressionSyntax _memberAccessValueResult;

    private WalkerResult _result = WalkerResult.Default;

    public ResultValueWalker(MemberAccessExpressionSyntax memberAccessValueResult)
    {
        _memberAccessValueResult = memberAccessValueResult;
    }

    public WalkerResult NodeWalker(SyntaxNode node)
    {
        _result = WalkerResult.Default;
        NodeWalkerInternal(node);
        return _result;
    }


    private void NodeWalkerInternal(SyntaxNode node)
    {
        foreach (var child in node.ChildNodes())
        {
            switch (child)
            {
                //Fix if(result.IsFailure || result.Value > 1) Console.WriteLine("foo"); is marked als inccorrect usage
                case IfStatementSyntax ifStatement:
                    _result.CheckResult = DetermineCheckResult(ifStatement.Condition);
                    NodeWalkerInternal(ifStatement);
                    if (_result.CorrectUsage) return;
                    if (_result is { CheckResult: CheckResult.CheckedSuccess, Terminated: true, AccessedValue: false })
                    {
                        _result.CheckResult = CheckResult.Unchecked;
                    }

                    _result.Terminated = false;
                    break;
                case ReturnStatementSyntax or ThrowStatementSyntax:
                    _result.Terminated = true;
                    NodeWalkerInternal(child);
                    return;
                case ConditionalExpressionSyntax ternary:
                    CheckTernaryCondition(ternary);

                    if (!_result.CorrectUsage) return;
                    break;
                case SwitchExpressionArmSyntax switchExpressionArmSyntax:
                    _result.CheckResult = CheckSwitchExpressionArm(switchExpressionArmSyntax);
                    if (_result.CorrectUsage) return;
                    break;
                case MemberAccessExpressionSyntax ma:
                    if (_memberAccessValueResult == ma)
                    {
                        _result.AccessedValue = true;
                        return;
                    }

                    break;
            }

            // Recursively analyze child nodes
            NodeWalkerInternal(child);
            if (_result.CorrectUsage) return;
        }
    }

    private void CheckTernaryCondition(ConditionalExpressionSyntax ternary)
    {
        _result.CheckResult = DetermineCheckResult(ternary.Condition);
        _result.Terminated = true;
        _result.AccessedValue = _result.CheckResult switch
        {
            CheckResult.CheckedSuccess => ternary.WhenTrue == _memberAccessValueResult && ternary.WhenFalse != _memberAccessValueResult,
            CheckResult.CheckedFailure => ternary.WhenTrue == _memberAccessValueResult,
            _ => _result.AccessedValue
        };
    }

    private CheckResult BinaryExpressionSyntax(BinaryExpressionSyntax binaryExpression)
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

                    if (rightResult == CheckResult.AccesedValue && leftResult == CheckResult.CheckedFailure)
                        return CheckResult.CheckedSuccess;

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
                    if (DetermineCheckResult(binaryExpression.Left) == CheckResult.AccesedValue ||
                        DetermineCheckResult(binaryExpression.Right) == CheckResult.AccesedValue)
                        return CheckResult.AccesedValue;
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
                var checkResultLeft = DetermineCheckResult(binaryExpression.Left);
                var checkResultRight = DetermineCheckResult(binaryExpression.Right);
                if (checkResultLeft == CheckResult.AccesedValue || checkResultRight == CheckResult.AccesedValue)
                    return CheckResult.AccesedValue;
                break;
        }

        return CheckResult.Unchecked;
    }

    private CheckResult DetermineCheckResult(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binaryExpression:
                return BinaryExpressionSyntax(binaryExpression);
            case MemberAccessExpressionSyntax memberAccess:
                {
                    if (memberAccess == _memberAccessValueResult) return CheckResult.AccesedValue;
                    switch (memberAccess.Name.ToString())
                    {
                        case "IsSuccess":
                            return CheckResult.CheckedSuccess;
                        case "IsFailure":
                            return CheckResult.CheckedFailure;
                    }

                    break;
                }
            case PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.Operand.ToString().Contains("IsSuccess"):
                return CheckResult.CheckedFailure; // This means we found a !IsSuccess, so it's equivalent to IsFailure.
            case PrefixUnaryExpressionSyntax prefixUnary when prefixUnary.Operand.ToString().Contains("IsFailure"):
                return CheckResult.CheckedSuccess; // This means we found a !IsFailure, so it's equivalent to IsSuccess.
            case ConditionalExpressionSyntax ternary:
                return DetermineCheckResult(ternary.Condition);
            case IsPatternExpressionSyntax isPatternExpressionSyntax:
                var info = DebugInfo(isPatternExpressionSyntax);
                return isPatternExpressionSyntax.Pattern switch
                {
                    RecursivePatternSyntax recursivePatternSyntax => CheckedRecusivePattern(recursivePatternSyntax),
                };
            case SwitchExpressionSyntax switchExpressionSyntax:
                throw new NotImplementedException();
        }

        return CheckResult.Unchecked;
    }

    private static string DebugInfo(ExpressionSyntax syntax)
    {
        var syntaxTree = syntax.SyntaxTree;
        var lineSpan = syntaxTree.GetLineSpan(syntax.Span);
        var startLineNumber = lineSpan.StartLinePosition.Line; // 0-based
        var startCharacterPosition = lineSpan.StartLinePosition.Character; // Ook 0-based
        var filePath = syntaxTree.FilePath;
        return $"File: {filePath} Line: {startLineNumber +1}, Char: {startCharacterPosition + 1}";
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
        return pattern is not RecursivePatternSyntax recursivePattern ? CheckResult.Unchecked : CheckedRecusivePattern(recursivePattern);
    }

    private static CheckResult CheckedRecusivePattern(RecursivePatternSyntax recursivePattern)
    {
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
}
