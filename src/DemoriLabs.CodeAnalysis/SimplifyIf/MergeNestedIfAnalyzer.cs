using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MergeNestedIfAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.MergeNestedIf,
        title: "Merge nested if statements",
        messageFormat: "Merge nested if statements",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Nested 'if' statements with no else clause can be merged into a single 'if' with a combined condition."
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

        if (ifStatement.Statement is not BlockSyntax { Statements.Count: 1 } block)
            return;

        if (block.Statements[0] is not IfStatementSyntax innerIf)
            return;

        if (innerIf.Else is not null)
            return;

        if (IsNestedInsideMergeableIf(ifStatement))
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

    private static bool IsNestedInsideMergeableIf(IfStatementSyntax ifStatement)
    {
        if (ifStatement.Parent is not BlockSyntax { Statements.Count: 1 } parentBlock)
            return false;

        if (parentBlock.Parent is not IfStatementSyntax parentIf)
            return false;

        return parentIf.Else is null;
    }
}
