using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BooleanReturnAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SimplifyBooleanReturn,
        title: "Simplify boolean return",
        messageFormat: "Simplify boolean return",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An 'if' statement that returns true/false in both branches can be simplified to a single return statement."
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

        if (TryGetSingleReturnExpression(ifStatement.Statement) is not { } ifReturn)
            return;

        if (TryGetBooleanLiteralValue(ifReturn) is not { } ifValue)
            return;

        bool? elseValue;

        if (ifStatement.Else is not null)
        {
            if (TryGetSingleReturnExpression(ifStatement.Else.Statement) is not { } elseReturn)
                return;

            elseValue = TryGetBooleanLiteralValue(elseReturn);
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

            elseValue = TryGetBooleanLiteralValue(nextExpr);
        }

        if (elseValue is null)
            return;

        if (ifValue == elseValue)
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

    private static bool? TryGetBooleanLiteralValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.TrueLiteralExpression } => true,
            LiteralExpressionSyntax { RawKind: (int)SyntaxKind.FalseLiteralExpression } => false,
            _ => null,
        };
    }
}
