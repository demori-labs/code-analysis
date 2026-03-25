using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.NullSimplification;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullConditionalAssignmentAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseNullConditionalAssignment,
        title: "Use null-conditional assignment",
        messageFormat: "Use null-conditional assignment",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A not-null check followed by a member access on the checked variable can be simplified to the '?.' operator."
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

        if (TryGetNotNullCheckVariable(ifStatement.Condition) is not { } variableName)
            return;

        if (TryGetSingleStatement(ifStatement.Statement) is not { } singleStatement)
            return;

        if (IsIncrementOrDecrement(singleStatement))
            return;

        if (HasMemberAccessOnVariable(singleStatement, variableName) is false)
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

    private static ExpressionStatementSyntax? TryGetSingleStatement(StatementSyntax statement)
    {
        return statement switch
        {
            ExpressionStatementSyntax expressionStatement => expressionStatement,
            BlockSyntax { Statements: [ExpressionStatementSyntax expressionStatement] } => expressionStatement,
            _ => null,
        };
    }

    private static bool IsIncrementOrDecrement(ExpressionStatementSyntax statement)
    {
        return statement.Expression
            is PostfixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression,
                }
                or PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression,
                };
    }

    private static bool HasMemberAccessOnVariable(ExpressionStatementSyntax statement, string variableName)
    {
        return statement.Expression switch
        {
            AssignmentExpressionSyntax assignment => GetReceiverIdentifier(assignment.Left) is { } id
                && id == variableName,
            InvocationExpressionSyntax invocation => GetReceiverIdentifier(invocation.Expression) is { } id
                && id == variableName,
            _ => false,
        };
    }

    private static string? GetReceiverIdentifier(ExpressionSyntax expression)
    {
        var current = expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            current = memberAccess.Expression;
        }

        return current is IdentifierNameSyntax identifier && current != expression ? identifier.Identifier.Text : null;
    }

    private static string? TryGetNotNullCheckVariable(ExpressionSyntax condition)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.NotEqualsExpression):
            {
                if (IsNullLiteral(binary.Right) && binary.Left is IdentifierNameSyntax leftId)
                    return leftId.Identifier.Text;
                if (IsNullLiteral(binary.Left) && binary.Right is IdentifierNameSyntax rightId)
                    return rightId.Identifier.Text;
                break;
            }
            case IsPatternExpressionSyntax
            {
                Pattern: UnaryPatternSyntax
                {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression: { } constExpr },
                },
                Expression: IdentifierNameSyntax identifier,
            } when IsNullLiteral(constExpr):
            {
                return identifier.Identifier.Text;
            }
        }

        return null;
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
    }
}
