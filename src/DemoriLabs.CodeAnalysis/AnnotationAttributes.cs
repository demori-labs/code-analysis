using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis;

internal static class AnnotationAttributes
{
    private const string AnnotationNamespace = "DemoriLabs.CodeAnalysis.Attributes";
    private const string MutableAttributeName = "MutableAttribute";
    private const string SuppressCognitiveComplexityAttributeName = "SuppressCognitiveComplexityAttribute";
    private const string CognitiveComplexityThresholdAttributeName = "CognitiveComplexityThresholdAttribute";
    private const string SuppressMultipleEnumerationAttributeName = "SuppressMultipleEnumerationAttribute";
    private const string NamedArgumentAttributeName = "NamedArgumentAttribute";
    private const string ReadOnlyAttributeName = "ReadOnlyAttribute";

    internal static bool HasMutableAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => IsAnnotationAttribute(a, MutableAttributeName));
    }

    internal static bool HasSuppressCognitiveComplexityAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => IsAnnotationAttribute(a, SuppressCognitiveComplexityAttributeName));
    }

    internal static bool HasSuppressMultipleEnumerationAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => IsAnnotationAttribute(a, SuppressMultipleEnumerationAttributeName));
    }

    internal static bool HasNamedArgumentAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => IsAnnotationAttribute(a, NamedArgumentAttributeName));
    }

    internal static bool HasReadOnlyAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a => IsAnnotationAttribute(a, ReadOnlyAttributeName));
    }

    internal static AttributeData? GetCognitiveComplexityThresholdAttribute(ISymbol symbol)
    {
        return symbol
            .GetAttributes()
            .FirstOrDefault(a => IsAnnotationAttribute(a, CognitiveComplexityThresholdAttributeName));
    }

    private static bool IsAnnotationAttribute(AttributeData attribute, string attributeName)
    {
        if (attribute.AttributeClass is not { } attributeClass)
            return false;

        return string.Equals(attributeClass.Name, attributeName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                attributeClass.ContainingNamespace?.ToDisplayString(),
                AnnotationNamespace,
                StringComparison.OrdinalIgnoreCase
            );
    }
}
