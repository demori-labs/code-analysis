using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsWithNullCheckAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseDeclarationPatternInsteadOfAs,
        title: "Use declaration pattern instead of 'as' with null check",
        messageFormat: "Use 'if ({0} is {1} {2})' instead of 'as' followed by null check",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When using 'as' followed by a null check, prefer a declaration pattern instead."
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
                analysisContext => AnalyzeLocalDeclaration(analysisContext, expressionType),
                SyntaxKind.LocalDeclarationStatement
            );
        });
    }

    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var localDeclaration = (LocalDeclarationStatementSyntax)context.Node;

        if (localDeclaration.Declaration.Variables.Count is not 1)
            return;

        var variable = localDeclaration.Declaration.Variables[0];

        if (
            variable.Initializer?.Value
            is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } asExpression
        )
        {
            return;
        }

        var variableName = variable.Identifier.Text;
        var sourceExpression = asExpression.Left;
        var targetType = asExpression.Right;

        if (localDeclaration.Parent is not BlockSyntax block)
            return;

        var statements = block.Statements;
        var declarationIndex = statements.IndexOf(localDeclaration);

        if (declarationIndex + 1 >= statements.Count)
            return;

        if (statements[declarationIndex + 1] is not IfStatementSyntax ifStatement)
            return;

        var isPositiveNullCheck = IsNotNullCheckOnVariable(ifStatement.Condition, variableName);
        var isGuardClause = isPositiveNullCheck is false && IsNullCheckOnVariable(ifStatement.Condition, variableName);

        switch (isPositiveNullCheck)
        {
            case false when isGuardClause is false:
            case true when IsVariableUsedAfterIf(block, declarationIndex + 1, variableName):
            case true when ifStatement.Else is not null && ContainsIdentifier(ifStatement.Else, variableName):
                return;
        }

        if (isGuardClause)
        {
            if (IfBodyAlwaysExits(ifStatement.Statement) is false)
                return;
        }

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                localDeclaration,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var asKeywordSpan = asExpression.OperatorToken.Span;
        var typeSpan = targetType.Span;
        var diagnosticSpan = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(asKeywordSpan.Start, typeSpan.End);
        var diagnosticLocation = Location.Create(asExpression.SyntaxTree, diagnosticSpan);

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, diagnosticLocation, sourceExpression, targetType, variableName)
        );
    }

    private static bool IsNotNullCheckOnVariable(ExpressionSyntax condition, string variableName)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } notEquals
                when IsVariableAndNull(notEquals, variableName):
            case IsPatternExpressionSyntax
            {
                Pattern: UnaryPatternSyntax
                {
                    Pattern: ConstantPatternSyntax
                    {
                        Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression },
                    },
                },
                Expression: IdentifierNameSyntax identifier,
            } when string.Equals(identifier.Identifier.Text, variableName, StringComparison.Ordinal):
                return true;
            default:
                return false;
        }
    }

    private static bool IsNullCheckOnVariable(ExpressionSyntax condition, string variableName)
    {
        switch (condition)
        {
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } equals
                when IsVariableAndNull(equals, variableName):
            case IsPatternExpressionSyntax
            {
                Pattern: ConstantPatternSyntax
                {
                    Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression },
                },
                Expression: IdentifierNameSyntax identifier,
            } when string.Equals(identifier.Identifier.Text, variableName, StringComparison.Ordinal):
                return true;
            default:
                return false;
        }
    }

    private static bool IfBodyAlwaysExits(StatementSyntax statement)
    {
        return statement switch
        {
            ThrowStatementSyntax or ReturnStatementSyntax or BreakStatementSyntax => true,
            BlockSyntax { Statements.Count: > 0 } block => block.Statements.Last()
                is ThrowStatementSyntax
                    or ReturnStatementSyntax
                    or BreakStatementSyntax,
            _ => false,
        };
    }

    private static bool IsVariableAndNull(BinaryExpressionSyntax binary, string variableName)
    {
        return (
                binary.Left is IdentifierNameSyntax left
                && left.Identifier.Text == variableName
                && binary.Right.IsKind(SyntaxKind.NullLiteralExpression)
            )
            || (
                binary.Right is IdentifierNameSyntax right
                && right.Identifier.Text == variableName
                && binary.Left.IsKind(SyntaxKind.NullLiteralExpression)
            );
    }

    private static bool IsVariableUsedAfterIf(BlockSyntax block, int ifIndex, string variableName)
    {
        var statements = block.Statements;
        for (var i = ifIndex + 1; i < statements.Count; i++)
        {
            if (ContainsIdentifier(statements[i], variableName))
                return true;
        }

        return false;
    }

    private static bool ContainsIdentifier(SyntaxNode node, string name)
    {
        foreach (var descendant in node.DescendantNodes())
        {
            if (descendant is IdentifierNameSyntax id && id.Identifier.Text == name)
                return true;
        }

        return false;
    }
}
