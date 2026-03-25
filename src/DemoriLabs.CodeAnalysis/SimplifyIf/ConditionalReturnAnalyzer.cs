using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalReturnAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SimplifyConditionalReturn,
        title: "Simplify conditional return to ternary",
        messageFormat: "Simplify conditional return to ternary",
        RuleCategories.Style,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "An 'if' statement that returns different values in both branches can be simplified to a single ternary return."
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

        ExpressionSyntax? elseReturn;

        if (ifStatement.Else is not null)
        {
            elseReturn = TryGetSingleReturnExpression(ifStatement.Else.Statement);
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

            elseReturn = nextExpr;
        }

        if (elseReturn is null)
            return;

        if (AreBothBooleanLiterals(ifReturn, elseReturn))
            return;

        if (ifReturn is ConditionalExpressionSyntax || elseReturn is ConditionalExpressionSyntax)
            return;

        if (
            ifReturn.WithoutTrivia().ToFullString().Length > 60
            || elseReturn.WithoutTrivia().ToFullString().Length > 60
        )
        {
            return;
        }

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

    private static bool AreBothBooleanLiterals(ExpressionSyntax a, ExpressionSyntax b)
    {
        return IsBooleanLiteral(a) && IsBooleanLiteral(b);
    }

    private static bool IsBooleanLiteral(ExpressionSyntax expression)
    {
        return expression
            is LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.TrueLiteralExpression or (int)SyntaxKind.FalseLiteralExpression,
            };
    }
}
