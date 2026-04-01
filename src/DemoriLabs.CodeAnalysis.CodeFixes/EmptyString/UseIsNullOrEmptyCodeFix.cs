using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.EmptyString;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class UseIsNullOrEmptyCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseIsNullOrEmpty];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: false);

        while (node is not null && IsFixableNode(node) is false)
        {
            node = node.Parent;
        }

        if (node is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use string.IsNullOrEmpty",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(UseIsNullOrEmptyCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null)
            return document;

        var replacementText = BuildReplacement(node, semanticModel, ct);
        if (replacementText is null)
            return document;

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(node, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static string? BuildReplacement(SyntaxNode node, SemanticModel semanticModel, CancellationToken ct)
    {
        return node switch
        {
            BinaryExpressionSyntax binary => BuildBinaryReplacement(binary, semanticModel, ct),
            IsPatternExpressionSyntax isPattern => BuildIsPatternReplacement(isPattern, semanticModel, ct),
            InvocationExpressionSyntax invocation => BuildInvocationReplacement(invocation, semanticModel, ct),
            _ => null,
        };
    }

    private static string? BuildBinaryReplacement(
        BinaryExpressionSyntax binary,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var isNegated = binary.IsKind(SyntaxKind.NotEqualsExpression);

        // str.Length == 0 / str?.Length == 0
        if (TryGetLengthReceiver(binary.Left, binary.Right, semanticModel, ct, out var receiver))
            return FormatResult(receiver, isNegated);

        if (TryGetLengthReceiver(binary.Right, binary.Left, semanticModel, ct, out receiver))
            return FormatResult(receiver, isNegated);

        // str == "" / "" == str / str == string.Empty
        if (IsEmptyStringExpr(binary.Left, semanticModel, ct))
            return FormatResult(binary.Right.WithoutTrivia().ToFullString(), isNegated);

        if (IsEmptyStringExpr(binary.Right, semanticModel, ct))
            return FormatResult(binary.Left.WithoutTrivia().ToFullString(), isNegated);

        return null;
    }

    private static string? BuildIsPatternReplacement(
        IsPatternExpressionSyntax isPattern,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var exprText = isPattern.Expression.WithoutTrivia().ToFullString();

        // str is "" / str is null or ""
        if (
            isPattern.Pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal }
            && IsEmptyStringLiteral(literal)
        )
        {
            return FormatResult(exprText, isNegated: false);
        }

        // str is not ""
        if (
            isPattern.Pattern
                is UnaryPatternSyntax
                {
                    RawKind: (int)SyntaxKind.NotPattern,
                    Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax notLiteral },
                }
            && IsEmptyStringLiteral(notLiteral)
        )
        {
            return FormatResult(exprText, isNegated: true);
        }

        // str is null or ""
        if (
            isPattern.Pattern
                is BinaryPatternSyntax
                {
                    RawKind: (int)SyntaxKind.OrPattern,
                    Left: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression },
                    Right: ConstantPatternSyntax { Expression: LiteralExpressionSyntax orLiteral },
                }
            && IsEmptyStringLiteral(orLiteral)
        )
        {
            return FormatResult(exprText, isNegated: false);
        }

        // str is not null and not ""
        if (
            isPattern.Pattern
                is BinaryPatternSyntax
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
                        Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax andLiteral },
                    },
                }
            && IsEmptyStringLiteral(andLiteral)
        )
        {
            return FormatResult(exprText, isNegated: true);
        }

        // str.Length is 0 / str.Length is not 0
        if (isPattern.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } lengthAccess)
        {
            var receiverType = semanticModel.GetTypeInfo(lengthAccess.Expression, ct).Type;
            if (receiverType?.SpecialType is SpecialType.System_String)
            {
                var receiverText = lengthAccess.Expression.WithoutTrivia().ToFullString();
                var isNegated = isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern };
                return FormatResult(receiverText, isNegated);
            }
        }

        return null;
    }

    private static string? BuildInvocationReplacement(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var symbol = semanticModel.GetSymbolInfo(invocation, ct).Symbol;
        if (
            symbol is not IMethodSymbol method
            || string.Equals(method.Name, "Equals", System.StringComparison.Ordinal) is false
            || method.ContainingType?.SpecialType is not SpecialType.System_String
        )
        {
            return null;
        }

        if (method.IsStatic && invocation.ArgumentList.Arguments.Count >= 2)
        {
            var arg0 = invocation.ArgumentList.Arguments[0].Expression;
            var arg1 = invocation.ArgumentList.Arguments[1].Expression;

            if (IsEmptyStringExpr(arg0, semanticModel, ct))
                return FormatResult(arg1.WithoutTrivia().ToFullString(), isNegated: false);

            if (IsEmptyStringExpr(arg1, semanticModel, ct))
                return FormatResult(arg0.WithoutTrivia().ToFullString(), isNegated: false);
        }
        else if (
            method.IsStatic is false
            && invocation.ArgumentList.Arguments.Count >= 1
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
        )
        {
            var arg0 = invocation.ArgumentList.Arguments[0].Expression;
            if (IsEmptyStringExpr(arg0, semanticModel, ct))
                return FormatResult(memberAccess.Expression.WithoutTrivia().ToFullString(), isNegated: false);
        }

        return null;
    }

    private static bool IsEmptyStringExpr(ExpressionSyntax expr, SemanticModel semanticModel, CancellationToken ct)
    {
        if (expr is LiteralExpressionSyntax literal && IsEmptyStringLiteral(literal))
            return true;

        if (expr is MemberAccessExpressionSyntax)
        {
            var symbol = semanticModel.GetSymbolInfo(expr, ct).Symbol;
            if (
                symbol is IFieldSymbol { Name: "Empty" }
                && symbol.ContainingType?.SpecialType is SpecialType.System_String
            )
            {
                return true;
            }
        }

        return false;
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

        if (zeroSide is not LiteralExpressionSyntax { Token.ValueText: "0" })
            return false;

        ExpressionSyntax? actualReceiver = null;

        if (lengthSide is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" } memberAccess)
        {
            actualReceiver = memberAccess.Expression;
        }
        else if (
            lengthSide is ConditionalAccessExpressionSyntax
            {
                WhenNotNull: MemberBindingExpressionSyntax { Name.Identifier.Text: "Length" },
            } conditionalAccess
        )
        {
            actualReceiver = conditionalAccess.Expression;
        }

        if (actualReceiver is null)
            return false;

        var type = semanticModel.GetTypeInfo(actualReceiver, ct).Type;
        if (type?.SpecialType is not SpecialType.System_String)
            return false;

        receiver = actualReceiver.WithoutTrivia().ToFullString();
        return true;
    }

    private static string FormatResult(string variableText, bool isNegated)
    {
        return isNegated ? $"string.IsNullOrEmpty({variableText}) is false" : $"string.IsNullOrEmpty({variableText})";
    }

    private static bool IsFixableNode(SyntaxNode node)
    {
        return node
            is BinaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
                }
                or IsPatternExpressionSyntax
                or InvocationExpressionSyntax;
    }
}
