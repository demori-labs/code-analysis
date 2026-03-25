using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.SimplifyIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConditionalAssignmentAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SimplifyConditionalAssignment,
        title: "Simplify conditional assignment to ternary",
        messageFormat: "Simplify conditional assignment to ternary",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An 'if' statement that assigns different values to the same variable in both branches can be simplified to a single ternary assignment."
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

        if (TryGetSingleSimpleAssignment(ifStatement.Statement) is not { } ifAssignment)
            return;

        if (TryGetSingleSimpleAssignment(ifStatement.Else.Statement) is not { } elseAssignment)
            return;

        if (ifAssignment.Left.ToFullString().Trim() != elseAssignment.Left.ToFullString().Trim())
            return;

        if (AreBothBooleanLiterals(ifAssignment.Right, elseAssignment.Right))
            return;

        if (ifAssignment.Right is ConditionalExpressionSyntax || elseAssignment.Right is ConditionalExpressionSyntax)
            return;

        if (
            ifAssignment.Right.WithoutTrivia().ToFullString().Length > 60
            || elseAssignment.Right.WithoutTrivia().ToFullString().Length > 60
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

    private static AssignmentExpressionSyntax? TryGetSingleSimpleAssignment(StatementSyntax statement)
    {
        var assignment = statement switch
        {
            ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax a } => a,
            BlockSyntax { Statements: [ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax a }] } => a,
            _ => null,
        };

        if (assignment is null)
            return null;

        return assignment.Kind() is not SyntaxKind.SimpleAssignmentExpression ? null : assignment;
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
