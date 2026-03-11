using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.RecordDesign;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RecordsShouldNotHaveMutablePropertiesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.RecordsShouldNotHaveMutableProperties,
        title: "Records should not have mutable properties",
        messageFormat: "Property '{0}' on record '{1}' should not have a setter; use 'init' instead",
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Record types are intended to be immutable. Properties should use 'init' accessors instead of 'set' accessors."
    );

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

        if (typeSymbol.IsValueType && !typeSymbol.IsReadOnly)
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

            if (property.SetMethod is not { } setMethod)
                continue;

            if (setMethod.IsInitOnly)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, property.Locations[0], property.Name, typeSymbol.Name));
        }
    }
}
