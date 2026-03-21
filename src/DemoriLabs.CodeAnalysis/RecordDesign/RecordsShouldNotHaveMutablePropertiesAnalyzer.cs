using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.RecordDesign;

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

    private static bool IsSetterRequiredByInterface(IPropertySymbol property, INamedTypeSymbol containingType)
    {
        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers(property.Name))
            {
                if (
                    member is IPropertySymbol { SetMethod: { IsInitOnly: false } } ifaceProp
                    && SymbolEqualityComparer.Default.Equals(
                        containingType.FindImplementationForInterfaceMember(ifaceProp),
                        property
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (!typeSymbol.IsRecord)
            return;

        if (typeSymbol is { IsValueType: true, IsReadOnly: false })
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

            if (property.SetMethod is not { IsInitOnly: false })
                continue;

            if (IsSetterRequiredByInterface(property, typeSymbol))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(Rule, property.Locations[0], property.Name, typeSymbol.Name));
        }
    }
}
