using System.Collections.Immutable;
using DemoriLabs.CodeAnalysis.PatternMatching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.EmptyString;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseIsNullOrEmptyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UseIsNullOrEmpty,
        title: "Use string.IsNullOrEmpty",
        messageFormat: "Use '{0}' instead of '{1}'",
        RuleCategories.Performance,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer string.IsNullOrEmpty over comparisons against empty strings or zero-length checks."
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

            var stringEmptyField = compilationContext
                .Compilation.GetSpecialType(SpecialType.System_String)
                .GetMembers("Empty")
                .OfType<IFieldSymbol>()
                .FirstOrDefault();

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeBinaryExpression(analysisContext, expressionType, stringEmptyField),
                SyntaxKind.EqualsExpression,
                SyntaxKind.NotEqualsExpression
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeIsPatternExpression(analysisContext, expressionType),
                SyntaxKind.IsPatternExpression
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeInvocationExpression(analysisContext, expressionType, stringEmptyField),
                SyntaxKind.InvocationExpression
            );
        });
    }

    private static void AnalyzeBinaryExpression(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? expressionType,
        IFieldSymbol? stringEmptyField
    )
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var left = binary.Left;
        var right = binary.Right;

        // Check for str == "" / str == string.Empty
        if (IsEmptyStringExpression(left, context.SemanticModel, stringEmptyField, context.CancellationToken))
        {
            if (IsInsideExpressionTree(binary, context, expressionType))
                return;

            var variableText = right.WithoutTrivia().ToFullString();
            ReportDiagnostic(context, binary, variableText, binary.IsKind(SyntaxKind.NotEqualsExpression));
            return;
        }

        if (IsEmptyStringExpression(right, context.SemanticModel, stringEmptyField, context.CancellationToken))
        {
            if (IsInsideExpressionTree(binary, context, expressionType))
                return;

            var variableText = left.WithoutTrivia().ToFullString();
            ReportDiagnostic(context, binary, variableText, binary.IsKind(SyntaxKind.NotEqualsExpression));
            return;
        }

        // Check for str.Length == 0 / str?.Length == 0
        if (TryGetLengthReceiver(left, right, context.SemanticModel, context.CancellationToken, out var receiver))
        {
            if (IsInsideExpressionTree(binary, context, expressionType))
                return;

            ReportDiagnostic(context, binary, receiver, binary.IsKind(SyntaxKind.NotEqualsExpression));
            return;
        }

        if (!TryGetLengthReceiver(right, left, context.SemanticModel, context.CancellationToken, out receiver))
            return;

        if (IsInsideExpressionTree(binary, context, expressionType))
            return;

        ReportDiagnostic(context, binary, receiver, binary.IsKind(SyntaxKind.NotEqualsExpression));
    }

    private static void AnalyzeIsPatternExpression(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var isPattern = (IsPatternExpressionSyntax)context.Node;

        // str is "" / str is not ""
        switch (isPattern.Pattern)
        {
            case ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal }
                when IsEmptyStringLiteral(literal):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var exprText = isPattern.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, exprText, isNegated: false);
                return;
            }
            case UnaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.NotPattern,
                Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal },
            } when IsEmptyStringLiteral(literal):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var exprText = isPattern.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, exprText, isNegated: true);
                return;
            }
        }

        switch (isPattern.Pattern)
        {
            // str is null or ""
            case BinaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.OrPattern,
                Left: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression },
                Right: ConstantPatternSyntax { Expression: LiteralExpressionSyntax rightLiteral },
            } when IsEmptyStringLiteral(rightLiteral):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var exprText = isPattern.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, exprText, isNegated: false);
                return;
            }
            // str is not null and not ""
            case BinaryPatternSyntax
            {
                RawKind: (int)SyntaxKind.AndPattern,
                Left: UnaryPatternSyntax
                {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression },
                },
                Right: UnaryPatternSyntax
                {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax andRightLiteral },
                },
            } when IsEmptyStringLiteral(andRightLiteral):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var exprText = isPattern.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, exprText, isNegated: true);
                return;
            }
        }

        switch (isPattern.Expression)
        {
            // str.Length is 0 / str.Length is not 0
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } lengthAccess
                when isPattern.Pattern
                    is ConstantPatternSyntax { Expression: LiteralExpressionSyntax { Token.ValueText: "0" } }
                    && IsStringType(
                        context.SemanticModel.GetTypeInfo(lengthAccess.Expression, context.CancellationToken).Type
                    ):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var receiverText = lengthAccess.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, receiverText, isNegated: false);
                return;
            }
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } lengthAccess2
                when isPattern.Pattern
                    is UnaryPatternSyntax
                    {
                        RawKind: (int)SyntaxKind.NotPattern,
                        Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { Token.ValueText: "0" } },
                    }
                    && IsStringType(
                        context.SemanticModel.GetTypeInfo(lengthAccess2.Expression, context.CancellationToken).Type
                    ):
            {
                if (IsInsideExpressionTree(isPattern, context, expressionType))
                    return;

                var receiverText = lengthAccess2.Expression.WithoutTrivia().ToFullString();
                ReportDiagnostic(context, isPattern, receiverText, isNegated: true);
                break;
            }
        }
    }

    private static void AnalyzeInvocationExpression(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? expressionType,
        IFieldSymbol? stringEmptyField
    )
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList.Arguments.Count is 0)
            return;

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
            return;

        if (
            string.Equals(method.Name, "Equals", StringComparison.Ordinal) is false
            || method.ContainingType?.SpecialType is not SpecialType.System_String
        )
        {
            return;
        }

        // Find the empty string argument and the variable
        string? variableText = null;

        if (method.IsStatic)
        {
            // string.Equals(str, "") or string.Equals("", str) — 2 or 3 args
            if (invocation.ArgumentList.Arguments.Count < 2)
                return;

            var arg0 = invocation.ArgumentList.Arguments[0].Expression;
            var arg1 = invocation.ArgumentList.Arguments[1].Expression;

            if (IsEmptyStringExpression(arg0, context.SemanticModel, stringEmptyField, context.CancellationToken))
            {
                variableText = arg1.WithoutTrivia().ToFullString();
            }
            else if (IsEmptyStringExpression(arg1, context.SemanticModel, stringEmptyField, context.CancellationToken))
            {
                variableText = arg0.WithoutTrivia().ToFullString();
            }
        }
        else
        {
            // str.Equals("") — 1 or 2 args
            var arg0 = invocation.ArgumentList.Arguments[0].Expression;
            if (IsEmptyStringExpression(arg0, context.SemanticModel, stringEmptyField, context.CancellationToken))
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    variableText = memberAccess.Expression.WithoutTrivia().ToFullString();
                }
            }
        }

        if (variableText is null)
            return;

        if (IsInsideExpressionTree(invocation, context, expressionType))
            return;

        ReportDiagnostic(context, invocation, variableText, isNegated: false);
    }

    private static bool IsEmptyStringExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        IFieldSymbol? stringEmptyField,
        CancellationToken ct
    )
    {
        if (expression is LiteralExpressionSyntax literal && IsEmptyStringLiteral(literal))
            return true;

        if (stringEmptyField is null || expression is not MemberAccessExpressionSyntax)
            return false;

        var symbol = semanticModel.GetSymbolInfo(expression, ct).Symbol;

        return symbol is not null && SymbolEqualityComparer.Default.Equals(symbol, stringEmptyField);
    }

    private static bool IsEmptyStringLiteral(LiteralExpressionSyntax literal)
    {
        return literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.Length is 0;
    }

    private static bool TryGetLengthReceiver(
        ExpressionSyntax lengthSide,
        ExpressionSyntax zeroSide,
        SemanticModel semanticModel,
        CancellationToken ct,
        out string receiver
    )
    {
        receiver = "";

        // Check the zero side is literal 0
        if (zeroSide is not LiteralExpressionSyntax { Token.ValueText: "0" })
            return false;

        // str.Length or str?.Length

        var actualReceiver = lengthSide switch
        {
            MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } memberAccess => memberAccess.Expression,
            ConditionalAccessExpressionSyntax
            {
                WhenNotNull: MemberBindingExpressionSyntax { Name.Identifier.Text: "Length" },
            } conditionalAccess => conditionalAccess.Expression,
            _ => null,
        };

        if (actualReceiver is null)
            return false;

        var type = semanticModel.GetTypeInfo(actualReceiver, ct).Type;
        if (IsStringType(type) is false)
            return false;

        receiver = actualReceiver.WithoutTrivia().ToFullString();
        return true;
    }

    private static bool IsStringType(ITypeSymbol? type)
    {
        return type?.SpecialType is SpecialType.System_String;
    }

    private static bool IsInsideExpressionTree(
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

    private static void ReportDiagnostic(
        SyntaxNodeAnalysisContext context,
        SyntaxNode node,
        string variableText,
        bool isNegated
    )
    {
        var suggestion = isNegated
            ? $"string.IsNullOrEmpty({variableText}) is false"
            : $"string.IsNullOrEmpty({variableText})";

        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation(), suggestion, node.ToString()));
    }
}
