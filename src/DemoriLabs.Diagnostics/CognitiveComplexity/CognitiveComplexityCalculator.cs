using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.Diagnostics.CognitiveComplexity;

internal sealed class CognitiveComplexityCalculator : CSharpSyntaxWalker
{
    private int _complexity;
    private int _nestingLevel;
    private bool _recursionDetected;
    private SemanticModel? _semanticModel;
    private IMethodSymbol? _method;

    private CognitiveComplexityCalculator() { }

    internal static int Calculate(SyntaxNode body, SemanticModel semanticModel, IMethodSymbol method)
    {
        var calculator = new CognitiveComplexityCalculator { _semanticModel = semanticModel, _method = method };
        calculator.Visit(body);
        return calculator._complexity;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        if (IsElseIf(node))
        {
            _complexity++;
        }
        else
        {
            IncrementWithNesting();
        }

        Visit(node.Condition);

        _nestingLevel++;
        Visit(node.Statement);
        _nestingLevel--;

        if (node.Else is null)
        {
            return;
        }

        if (node.Else.Statement is IfStatementSyntax)
        {
            Visit(node.Else.Statement);
        }
        else
        {
            _complexity++;
            _nestingLevel++;
            Visit(node.Else.Statement);
            _nestingLevel--;
        }
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitConditionalExpression(node);
        _nestingLevel--;
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitSwitchStatement(node);
        _nestingLevel--;
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitSwitchExpression(node);
        _nestingLevel--;
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitForStatement(node);
        _nestingLevel--;
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitForEachStatement(node);
        _nestingLevel--;
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitWhileStatement(node);
        _nestingLevel--;
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitDoStatement(node);
        _nestingLevel--;
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        IncrementWithNesting();
        _nestingLevel++;
        base.VisitCatchClause(node);
        _nestingLevel--;
    }

    public override void VisitGotoStatement(GotoStatementSyntax node)
    {
        _complexity++;
        base.VisitGotoStatement(node);
    }

    public override void VisitCatchFilterClause(CatchFilterClauseSyntax node)
    {
        _complexity++;
        base.VisitCatchFilterClause(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var isNewSequence =
            IsLogicalOperator(node.Kind())
            && (!IsLogicalOperator(node.Left) || GetLogicalOperatorKind(node.Left) != node.Kind());

        if (isNewSequence)
        {
            _complexity++;
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (
            !_recursionDetected
            && _semanticModel is not null
            && _method is not null
            && SymbolEqualityComparer.Default.Equals(
                _semanticModel.GetSymbolInfo(node).Symbol?.OriginalDefinition,
                _method.OriginalDefinition
            )
        )
        {
            _recursionDetected = true;
            _complexity++;
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        _nestingLevel++;
        base.VisitSimpleLambdaExpression(node);
        _nestingLevel--;
    }

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        _nestingLevel++;
        base.VisitParenthesizedLambdaExpression(node);
        _nestingLevel--;
    }

    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        _nestingLevel++;
        base.VisitAnonymousMethodExpression(node);
        _nestingLevel--;
    }

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        _nestingLevel++;
        base.VisitLocalFunctionStatement(node);
        _nestingLevel--;
    }

    private void IncrementWithNesting()
    {
        _complexity += 1 + _nestingLevel;
    }

    private static bool IsElseIf(IfStatementSyntax node)
    {
        return node.Parent is ElseClauseSyntax;
    }

    private static bool IsLogicalOperator(SyntaxKind kind)
    {
        return kind is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression;
    }

    private static bool IsLogicalOperator(ExpressionSyntax expression)
    {
        return expression is BinaryExpressionSyntax binary && IsLogicalOperator(binary.Kind());
    }

    private static SyntaxKind GetLogicalOperatorKind(ExpressionSyntax expression)
    {
        return expression is BinaryExpressionSyntax binary ? binary.Kind() : SyntaxKind.None;
    }
}
