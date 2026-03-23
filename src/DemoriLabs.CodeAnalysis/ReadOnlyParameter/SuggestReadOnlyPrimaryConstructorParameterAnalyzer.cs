using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.ReadOnlyParameter;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuggestReadOnlyPrimaryConstructorParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor SuggestReadOnlyRule = new(
        RuleIdentifiers.SuggestReadOnlyPrimaryConstructorParameter,
        title: "Primary constructor parameter should be [ReadOnly]",
        messageFormat: "Consider adding [ReadOnly] to primary constructor parameter '{0}'",
        RuleCategories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Primary constructor parameters on classes should be marked with [ReadOnly] to prevent accidental reassignment."
    );

    private static readonly DiagnosticDescriptor MutableIncompatibleRule = new(
        RuleIdentifiers.MutableIncompatibleModifier,
        title: "[Mutable] is not valid on this parameter",
        messageFormat: "[Mutable] on parameter '{0}' has no effect because the backing field is already readonly",
        RuleCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Primary constructor parameters on record classes, readonly structs, and readonly record structs are already immutable. [Mutable] has no effect."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [SuggestReadOnlyRule, MutableIncompatibleRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;

        if (parameter.Parent?.Parent is not TypeDeclarationSyntax typeDecl)
        {
            return;
        }

        var isAlreadyReadOnly = typeDecl switch
        {
            RecordDeclarationSyntax => true,
            StructDeclarationSyntax when typeDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) => true,
            ClassDeclarationSyntax or StructDeclarationSyntax => false,
            _ => true, // unknown type — skip
        };

        if (isAlreadyReadOnly)
        {
            // [Mutable] is meaningless on already-readonly parameters
            var mutableAttr = FindMutableAttribute(parameter);
            if (mutableAttr is not null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(MutableIncompatibleRule, mutableAttr.GetLocation(), parameter.Identifier.Text)
                );
            }

            return;
        }

        if (
            parameter.Modifiers.Any(m =>
                m.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword
            )
        )
        {
            return;
        }

        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken)!;

        if (AnnotationAttributes.HasReadOnlyAttribute(parameterSymbol))
            return;

        if (AnnotationAttributes.HasMutableAttribute(parameterSymbol))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(SuggestReadOnlyRule, parameter.Identifier.GetLocation(), parameter.Identifier.Text)
        );
    }

    private static AttributeSyntax? FindMutableAttribute(ParameterSyntax parameter)
    {
        foreach (var attrList in parameter.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "Mutable" or "MutableAttribute")
                {
                    return attr;
                }
            }
        }

        return null;
    }
}
