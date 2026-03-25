using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BooleanAssignmentAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SimplifyBooleanAssignment,
        title: "Simplify boolean assignment",
        messageFormat: "Simplify boolean assignment",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An 'if' statement that assigns true/false in both branches can be simplified to a single assignment."
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

        if (ifStatement.Else is null)
            return;

        if (TryGetSingleAssignment(ifStatement.Statement) is not { } ifAssignment)
            return;

        if (TryGetBooleanLiteralValue(ifAssignment.Right) is not { } ifValue)
            return;

        if (TryGetSingleAssignment(ifStatement.Else.Statement) is not { } elseAssignment)
            return;

        if (TryGetBooleanLiteralValue(elseAssignment.Right) is not { } elseValue)
            return;

        if (ifValue == elseValue)
            return;

        if (ifAssignment.Left.ToFullString().Trim() != elseAssignment.Left.ToFullString().Trim())
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
