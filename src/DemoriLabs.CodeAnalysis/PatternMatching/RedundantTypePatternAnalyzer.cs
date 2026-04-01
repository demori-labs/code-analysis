using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantTypePatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.RedundantTypePattern,
        title: "Type pattern is redundant",
        messageFormat: "Type check 'is {0}' is redundant because the expression is already of type '{1}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The type pattern always matches because the expression's compile-time type already guarantees the check succeeds."
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
                analysisContext => AnalyzeIsExpression(analysisContext, expressionType),
                SyntaxKind.IsExpression
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeIsPatternExpression(analysisContext, expressionType),
                SyntaxKind.IsPatternExpression
            );
        });
    }

    private static void AnalyzeIsExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isExpression = (BinaryExpressionSyntax)context.Node;
        var checkedExpression = isExpression.Left;
        var checkedTypeSyntax = isExpression.Right;

        if (ShouldSkipExpressionTree(isExpression, context, expressionType))
            return;

        var exprTypeInfo = context.SemanticModel.GetTypeInfo(checkedExpression, context.CancellationToken);
        var checkedTypeSymbol = context.SemanticModel.GetTypeInfo(checkedTypeSyntax, context.CancellationToken).Type;

        if (
            IsRedundant(exprTypeInfo, checkedTypeSymbol, context.SemanticModel.Compilation, isDeclarationPattern: false)
        )
        {
            var checkedTypeName = checkedTypeSyntax.ToString();
            var exprTypeName = exprTypeInfo.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, isExpression.GetLocation(), checkedTypeName, exprTypeName)
            );
        }
    }

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;

        if (isPattern.Pattern is not DeclarationPatternSyntax declarationPattern)
            return;

        var checkedExpression = isPattern.Expression;

        if (ShouldSkipExpressionTree(isPattern, context, expressionType))
            return;

        var exprTypeInfo = context.SemanticModel.GetTypeInfo(checkedExpression, context.CancellationToken);
        var checkedTypeSymbol =
            context.SemanticModel.GetSymbolInfo(declarationPattern.Type, context.CancellationToken).Symbol
            as ITypeSymbol;

        if (IsRedundant(exprTypeInfo, checkedTypeSymbol, context.SemanticModel.Compilation, isDeclarationPattern: true))
        {
            var checkedTypeName = declarationPattern.Type.ToString();
            var exprTypeName = exprTypeInfo.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(Diagnostic.Create(Rule, isPattern.GetLocation(), checkedTypeName, exprTypeName));
        }
    }

    private static bool IsRedundant(
        TypeInfo exprTypeInfo,
        ITypeSymbol? checkedTypeSymbol,
        Compilation compilation,
        bool isDeclarationPattern
    )
    {
        var exprType = exprTypeInfo.Type;
        if (exprType is null || checkedTypeSymbol is null)
            return false;

        var conversion = compilation.ClassifyConversion(exprType, checkedTypeSymbol);
        if (conversion.IsIdentity is false && (conversion.IsImplicit && conversion.IsReference) is false)
            return false;

        var flowState = exprTypeInfo.Nullability.FlowState;

        if (exprType.IsValueType)
            return true;

        // NRT not active for this code — skip reference types
        if (flowState is NullableFlowState.None)
            return false;

        // Nullable reference: is Type variable is the correct idiom, don't flag
        if (flowState is NullableFlowState.MaybeNull && isDeclarationPattern)
            return false;

        return true;
    }

    private static bool ShouldSkipExpressionTree(
        SyntaxNode node,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? expressionType
    )
    {
        return expressionType is not null
            && ExpressionTreeHelper.IsInsideExpressionTree(
                node,
                context.SemanticModel,
                expressionType,
                context.CancellationToken
            );
    }
}
