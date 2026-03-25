using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.NullSimplification;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullCoalescingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseNullCoalescing,
        title: "Use null-coalescing operator",
        messageFormat: "Use null-coalescing operator",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A null check with a ternary or if-return can be simplified to the '??' operator."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var expressionType = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Linq.Expressions.Expression`1"
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeConditionalExpression(analysisContext, expressionType),
                SyntaxKind.ConditionalExpression
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeIfStatement(analysisContext, expressionType),
                SyntaxKind.IfStatement
            );
        });
    }

    private static void AnalyzeConditionalExpression(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? expressionType
    )
    {
        var conditional = (ConditionalExpressionSyntax)context.Node;

        if (TryGetNullCheckVariable(conditional.Condition) is not var (variable, isNotNull))
            return;

        var whenTrue = conditional.WhenTrue.WithoutTrivia();
        var whenFalse = conditional.WhenFalse.WithoutTrivia();

        if (isNotNull)
        {
            if (AreEquivalent(whenTrue, variable) is false)
                return;
        }
        else
        {
            if (AreEquivalent(whenFalse, variable) is false)
                return;
        }

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                conditional,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, conditional.QuestionToken.GetLocation()));
    }

    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        if (TryGetNullCheckVariable(ifStatement.Condition) is not var (variable, isNotNull))
            return;

        if (isNotNull is false)
            return;

        if (TryGetSingleReturnExpression(ifStatement.Statement) is not { } ifReturn)
            return;

        if (AreEquivalent(ifReturn.WithoutTrivia(), variable) is false)
            return;

        ExpressionSyntax? fallbackExpr;

        if (ifStatement.Else is not null)
        {
            fallbackExpr = TryGetSingleReturnExpression(ifStatement.Else.Statement);
        }
        else
        {
            if (ifStatement.Parent is not BlockSyntax parentBlock)
                return;

            var ifIndex = parentBlock.Statements.IndexOf(ifStatement);
            if (ifIndex is -1 || ifIndex != parentBlock.Statements.Count - 2)
                return;

            var nextStatement = parentBlock.Statements[ifIndex + 1];
            if (nextStatement is not ReturnStatementSyntax { Expression: { } nextExpr })
                return;

            fallbackExpr = nextExpr;
        }

        if (fallbackExpr is null)
            return;

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                ifStatement,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.IfKeyword.GetLocation()));
    }

    private static ExpressionSyntax? TryGetSingleReturnExpression(StatementSyntax statement)
    {
        return statement switch
        {
            ReturnStatementSyntax { Expression: { } expr } => expr,
            BlockSyntax { Statements: [ReturnStatementSyntax { Expression: { } expr }] } => expr,
            _ => null,
        };
    }

    private static (ExpressionSyntax Variable, bool IsNotNull)? TryGetNullCheckVariable(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binary:
            {
                if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                {
                    if (IsNullLiteral(binary.Right) && IsSimpleIdentifier(binary.Left))
                        return (binary.Left, IsNotNull: true);
                    if (IsNullLiteral(binary.Left) && IsSimpleIdentifier(binary.Right))
                        return (binary.Right, IsNotNull: true);
                }
                else if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsNullLiteral(binary.Right) && IsSimpleIdentifier(binary.Left))
                        return (binary.Left, IsNotNull: false);
                    if (IsNullLiteral(binary.Left) && IsSimpleIdentifier(binary.Right))
                        return (binary.Right, IsNotNull: false);
                }

                break;
            }
            case IsPatternExpressionSyntax isPattern when IsSimpleIdentifier(isPattern.Expression):
            {
                switch (isPattern.Pattern)
                {
                    case ConstantPatternSyntax { Expression: { } constExpr } when IsNullLiteral(constExpr):
                        return (isPattern.Expression, IsNotNull: false);
                    case UnaryPatternSyntax
                    {
                        OperatorToken.RawKind: (int)SyntaxKind.NotKeyword,
                        Pattern: ConstantPatternSyntax { Expression: { } innerConst },
                    } when IsNullLiteral(innerConst):
                        return (isPattern.Expression, IsNotNull: true);
                }

                break;
            }
        }

        return null;
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }

    private static bool IsSimpleIdentifier(ExpressionSyntax expression)
    {
        return expression is IdentifierNameSyntax;
    }

    private static bool AreEquivalent(ExpressionSyntax expression, ExpressionSyntax variable)
    {
        return SyntaxFactory.AreEquivalent(expression, variable);
    }
}
