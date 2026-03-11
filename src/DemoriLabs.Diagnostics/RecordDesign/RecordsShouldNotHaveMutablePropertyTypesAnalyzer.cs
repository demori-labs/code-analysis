using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.RecordDesign;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordsShouldNotHaveMutablePropertyTypesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.RecordsShouldNotHaveMutablePropertyTypes,
        title: "Records should not have mutable property types",
        messageFormat: "Property '{0}' on record '{1}' uses mutable type '{2}'; use an immutable or read-only type instead",
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Record types are intended to be immutable. Properties should use immutable or read-only collection types."
    );

    private static readonly FrozenSet<string> MutableCollectionTypeNames = new HashSet<string>
    {
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.SortedSet`1",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.Stack`1",
        "System.Collections.Generic.LinkedList`1",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.SortedList`2",
        "System.Collections.ArrayList",
        "System.Collections.Hashtable",
        "System.Collections.ObjectModel.Collection`1",
        "System.Collections.ObjectModel.ObservableCollection`1",
        "System.Collections.Concurrent.ConcurrentDictionary`2",
        "System.Collections.Concurrent.ConcurrentBag`1",
        "System.Collections.Concurrent.ConcurrentQueue`1",
        "System.Collections.Concurrent.ConcurrentStack`1",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (!typeSymbol.IsRecord)
            return;

        if (AnnotationAttributes.HasMutableAttribute(typeSymbol))
            return;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            if (property.IsImplicitlyDeclared)
                continue;

            if (AnnotationAttributes.HasMutableAttribute(property))
                continue;

            if (IsMutableType(property.Type))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Rule,
                        property.Locations[0],
                        property.Name,
                        typeSymbol.Name,
                        property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                    )
                );
            }
        }
    }

    private static bool IsMutableType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        var current = type as INamedTypeSymbol;
        while (current is not null)
        {
            var metadataName = GetFullMetadataName(current.ConstructedFrom);
            if (MutableCollectionTypeNames.Contains(metadataName))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    private static string GetFullMetadataName(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace;
        if (ns is null || ns.IsGlobalNamespace)
            return symbol.MetadataName;

        return ns.ToDisplayString() + "." + symbol.MetadataName;
    }
}
