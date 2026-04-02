using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.PatternMatching;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class NegationPatternCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseNegationPattern];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression })
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use pattern matching",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(NegationPatternCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> FixAsync(Document document, SyntaxNode node, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null || node is not PrefixUnaryExpressionSyntax prefixUnary)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);

        var operand = prefixUnary.Operand;

        var unwrapped = operand;
        while (unwrapped is ParenthesizedExpressionSyntax paren)
            unwrapped = paren.Expression;

        string replacementText;

        switch (unwrapped)
        {
            // !(x is not Y) → x is Y, !(x is Y) → x is not Y
            case IsPatternExpressionSyntax isPattern:
            {
                var expr = isPattern.Expression.WithoutTrivia().ToFullString();
                if (isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern } notPattern)
                {
                    replacementText = $"{expr} is {notPattern.Pattern.WithoutTrivia().ToFullString()}";
                }
                else
                {
                    replacementText = $"{expr} is not {isPattern.Pattern.WithoutTrivia().ToFullString()}";
                }

                break;
            }

            // !(x is T) — old-style IsExpression → x is not T
            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.IsExpression } isExpr:
            {
                var expr = isExpr.Left.WithoutTrivia().ToFullString();
                var type = isExpr.Right.WithoutTrivia().ToFullString();
                replacementText = $"{expr} is not {type}";
                break;
            }

            // !(x == c) → x is not c, !(x != c) → x is c
            case BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression,
            } comparison:
            {
                var leftIsLiteral =
                    comparison.Left
                    is LiteralExpressionSyntax
                        or PrefixUnaryExpressionSyntax
                        {
                            RawKind: (int)SyntaxKind.UnaryMinusExpression,
                            Operand: LiteralExpressionSyntax,
                        }
                        or MemberAccessExpressionSyntax;
                var innerVariable = leftIsLiteral
                    ? comparison.Right.WithoutTrivia().ToFullString()
                    : comparison.Left.WithoutTrivia().ToFullString();
                var innerConstant = leftIsLiteral
                    ? comparison.Left.WithoutTrivia().ToFullString()
                    : comparison.Right.WithoutTrivia().ToFullString();
                var isEquality = comparison.IsKind(SyntaxKind.EqualsExpression);
                replacementText = isEquality
                    ? $"{innerVariable} is not {innerConstant}"
                    : $"{innerVariable} is {innerConstant}";
                break;
            }

            // !id.HasValue → id is null (when Nullable<T>)
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "HasValue" } hasValueAccess
                when semanticModel is not null
                    && semanticModel.GetTypeInfo(hasValueAccess.Expression, ct).Type?.OriginalDefinition.SpecialType
                        is SpecialType.System_Nullable_T:
            {
                var ownerText = hasValueAccess.Expression.WithoutTrivia().ToFullString();
                replacementText = $"{ownerText} is null";
                break;
            }

            // !flag → flag is false
            default:
            {
                var operandText = operand.WithoutTrivia().ToFullString();
                replacementText = $"{operandText} is false";
                break;
            }
        }

        var replacement = SyntaxFactory
            .ParseExpression(replacementText)
            .WithLeadingTrivia(prefixUnary.GetLeadingTrivia())
            .WithTrailingTrivia(prefixUnary.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(prefixUnary, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
