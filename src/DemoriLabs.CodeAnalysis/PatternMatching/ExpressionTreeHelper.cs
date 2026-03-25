using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

internal static class ExpressionTreeHelper
{
    internal static bool IsInsideExpressionTree(
        SyntaxNode node,
        SemanticModel semanticModel,
        INamedTypeSymbol expressionType,
        CancellationToken cancellationToken
    )
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is not LambdaExpressionSyntax and not AnonymousMethodExpressionSyntax)
                continue;

            var typeInfo = semanticModel.GetTypeInfo(current, cancellationToken);
            var convertedType = typeInfo.ConvertedType?.OriginalDefinition;

            if (convertedType is not null && SymbolEqualityComparer.Default.Equals(convertedType, expressionType))
            {
                return true;
            }
        }

        return false;
    }
}
