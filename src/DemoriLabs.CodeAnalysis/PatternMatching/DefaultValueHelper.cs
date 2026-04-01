using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.PatternMatching;

internal static class DefaultValueHelper
{
    internal static bool IsDefaultExpression(ExpressionSyntax expression)
    {
        return expression.IsKind(SyntaxKind.DefaultLiteralExpression) || expression is DefaultExpressionSyntax;
    }

    internal static string ResolveDefaultPatternText(
        ExpressionSyntax variable,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var type = semanticModel.GetTypeInfo(variable, ct).Type;

        if (type is null || type.IsReferenceType)
            return "null";

        if (type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T)
            return "null";

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => "false",
            SpecialType.System_Char => "'\\0'",
            SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Decimal => "0",
            _ when type.TypeKind is TypeKind.Enum => "0",
            _ => "default",
        };
    }
}
