using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.RecordDesign;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataClassCouldBeRecordAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
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

        if (HasIncompatibleInterface(typeSymbol))
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

    private static bool HasIncompatibleInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            // IEquatable<T> is auto-implemented by records
            if (
                iface is { IsGenericType: true, OriginalDefinition.Name: "IEquatable" }
                && iface.OriginalDefinition.ContainingNamespace?.ToDisplayString() is "System"
            )
            {
                continue;
            }

            foreach (var member in iface.GetMembers())
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary })
                    return true;

                if (member is IPropertySymbol { SetMethod: { IsInitOnly: false } })
                    return true;
            }
        }

        return false;
    }

    private static bool IsBehaviourMethod(IMethodSymbol method)
    {
        if (method.MethodKind is MethodKind.Conversion or MethodKind.Destructor)
            return true;

        if (method.MethodKind is MethodKind.UserDefinedOperator)
            return IsRecordSynthesisableOperator(method) is false;

        if (method.MethodKind is not MethodKind.Ordinary)
            return false;

        if (method.IsStatic)
            return false;

        return IsRecordSynthesisableMethod(method) is false;
    }

    private static bool IsRecordSynthesisableMethod(IMethodSymbol method)
    {
        return method.Name is "Equals" or "GetHashCode" or "ToString" or "Deconstruct" or "PrintMembers";
    }

    private static bool IsRecordSynthesisableOperator(IMethodSymbol method)
    {
        return method.Name is "op_Equality" or "op_Inequality";
    }

    private static bool IsPartialClass(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax() is ClassDeclarationSyntax classDecl
            && classDecl.Modifiers.Any(m => m.Kind() is SyntaxKind.PartialKeyword)
        );
    }
}
