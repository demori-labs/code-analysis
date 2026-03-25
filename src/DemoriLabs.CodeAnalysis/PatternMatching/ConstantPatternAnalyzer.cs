using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstantPatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseConstantPattern,
        title: "Use constant pattern",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer pattern matching with 'is' over equality operators when comparing against constants."
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
                analysisContext => AnalyzeBinaryExpression(analysisContext, expressionType),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression
            );
        });
    }

    private static void AnalyzeBinaryExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var binaryExpression = (BinaryExpressionSyntax)context.Node;

        var left = binaryExpression.Left;
        var right = binaryExpression.Right;

        var leftIsConstant = IsConstantExpression(left, context.SemanticModel, context.CancellationToken);
        var rightIsConstant = IsConstantExpression(right, context.SemanticModel, context.CancellationToken);

        if (leftIsConstant == rightIsConstant)
            return;

        var constant = leftIsConstant ? left : right;

        // Skip non-null string comparisons — a dedicated analyser will suggest string.Equals with StringComparison
        if (
            constant is LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.StringLiteralExpression or (int)SyntaxKind.Utf8StringLiteralExpression,
            }
        )
        {
            return;
        }

        if (
            expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                binaryExpression,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            )
        )
        {
            return;
        }

        var isEquals = binaryExpression.IsKind(SyntaxKind.EqualsExpression);
        var variable = leftIsConstant ? right : left;

        var constantText = constant.ToString();
        var variableText = variable.ToString();

        var suggestion = isEquals ? $"{variableText} is {constantText}" : $"{variableText} is not {constantText}";

        var originalText = binaryExpression.ToString();

        var operatorSpan = binaryExpression.OperatorToken.Span;
        var constantSpan = constant.Span;
        var start = Math.Min(operatorSpan.Start, constantSpan.Start);
        var end = Math.Max(operatorSpan.End, constantSpan.End);
        var diagnosticSpan = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(start, end);
        var diagnosticLocation = Location.Create(binaryExpression.SyntaxTree, diagnosticSpan);

        context.ReportDiagnostic(Diagnostic.Create(Rule, diagnosticLocation, suggestion, originalText));
    }

    private static bool IsConstantExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        while (expression is ParenthesizedExpressionSyntax paren)
            expression = paren.Expression;

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
            return true;

        switch (expression)
        {
            case LiteralExpressionSyntax:
            case PrefixUnaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.UnaryMinusExpression,
                Operand: LiteralExpressionSyntax,
            }:
                return true;
            default:
            {
                var constantValue = semanticModel.GetConstantValue(expression, cancellationToken);
                return constantValue.HasValue;
            }
        }
    }
}
