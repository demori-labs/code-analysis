using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.NullSimplification;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullCoalescingAssignmentAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseNullCoalescingAssignment,
        title: "Use null-coalescing assignment",
        messageFormat: "Use null-coalescing assignment",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A null check followed by assignment to the same variable can be simplified to the '??=' operator."
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
                analysisContext => AnalyzeIfStatement(analysisContext, expressionType),
                SyntaxKind.IfStatement
            );
        });
    }

    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        if (ifStatement.Else is not null)
            return;

        if (TryGetNullCheckVariable(ifStatement.Condition) is not { } nullCheck)
            return;

        if (nullCheck.IsNotNull)
            return;

        var variableText = nullCheck.Variable.WithoutTrivia().ToFullString();

        if (TryGetSingleAssignment(ifStatement.Statement) is not { } assignment)
            return;

        if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) is false)
            return;

        var targetText = assignment.Left.WithoutTrivia().ToFullString();

        if (variableText != targetText)
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

    private static AssignmentExpressionSyntax? TryGetSingleAssignment(StatementSyntax statement)
    {
        return statement switch
        {
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } => assignment,
            BlockSyntax
            {
                Statements: [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }],
            } => assignment,
            _ => null,
        };
    }

    private static (ExpressionSyntax Variable, bool IsNotNull)? TryGetNullCheckVariable(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binary:
            {
                if (binary.IsKind(SyntaxKind.EqualsExpression))
                {
                    if (IsNullLiteral(binary.Right))
                        return (binary.Left, IsNotNull: false);
                    if (IsNullLiteral(binary.Left))
                        return (binary.Right, IsNotNull: false);
                }

                break;
            }
            case IsPatternExpressionSyntax isPattern:
            {
                if (
                    isPattern.Pattern is ConstantPatternSyntax { Expression: { } constExpr }
                    && IsNullLiteral(constExpr)
                )
                {
                    return (isPattern.Expression, IsNotNull: false);
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
}
