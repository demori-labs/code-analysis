using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.RecordDesign;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataClassCouldBeRecordAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.DataClassCouldBeRecord,
        title: "Data class could be a record",
        messageFormat: "Class '{0}' only defines data members and could be converted to a record with immutable properties",
        RuleCategories.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Classes that only define properties without behaviour are better expressed as records with immutable properties."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        if (
            typeSymbol.TypeKind is not TypeKind.Class
            || typeSymbol.IsRecord
            || typeSymbol.IsStatic
            || typeSymbol.IsAbstract
        )
        {
            return;
        }

        if (typeSymbol.BaseType is not null && typeSymbol.BaseType.SpecialType is not SpecialType.System_Object)
        {
            return;
        }

        if (AnnotationAttributes.HasMutableAttribute(typeSymbol))
        {
            return;
        }

        if (IsPartialClass(typeSymbol))
        {
            return;
        }

        var hasProperties = false;

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            switch (member)
            {
                case IPropertySymbol:
                    hasProperties = true;
                    break;
                case IMethodSymbol method when IsBehaviourMethod(method):
                case IFieldSymbol:
                case IEventSymbol:
                    return;
            }
        }

        if (!hasProperties)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, typeSymbol.Locations[0], typeSymbol.Name));
    }

    private static bool IsBehaviourMethod(IMethodSymbol method)
    {
        return method.MethodKind
            is MethodKind.Ordinary
                or MethodKind.Conversion
                or MethodKind.Destructor
                or MethodKind.UserDefinedOperator;
    }

    private static bool IsPartialClass(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax() is ClassDeclarationSyntax classDecl
            && classDecl.Modifiers.Any(m => m.Kind() is SyntaxKind.PartialKeyword)
        );
    }
}
