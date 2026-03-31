using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.ReadOnlyParameter;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadOnlyParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.ReadOnlyParameter,
        title: "Parameter must not be reassigned",
        messageFormat: "Parameter '{0}' is marked [ReadOnly] and must not be reassigned",
        RuleCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Parameters marked with [ReadOnly] cannot be reassigned within the method body. Introduce a new local variable instead."
    );

    private static readonly DiagnosticDescriptor IncompatibleModifierRule = new(
        RuleIdentifiers.IncompatibleAttributeModifier,
        title: "Incompatible attribute on parameter",
        messageFormat: "{0}",
        RuleCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ReadOnly] and [Mutable] cannot be combined. [ReadOnly] cannot be applied to ref/out/in parameters. [Mutable] has no effect on parameters that are already readonly (records, readonly structs)."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, IncompatibleModifierRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(
            AnalyzeNode,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.PreIncrementExpression,
            SyntaxKind.PreDecrementExpression,
            SyntaxKind.PostIncrementExpression,
            SyntaxKind.PostDecrementExpression
        );
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var parameter = (ParameterSyntax)context.Node;
        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken);
        if (parameterSymbol is null)
            return;

        var hasReadOnly = AnnotationAttributes.HasReadOnlyAttribute(parameterSymbol);
        var hasMutable = AnnotationAttributes.HasMutableAttribute(parameterSymbol);

        switch (hasReadOnly)
        {
            // [ReadOnly] + [Mutable] conflict
            case true when hasMutable:
            {
                var readOnlyAttr = FindReadOnlyAttributeSyntax(
                    parameter,
                    context.SemanticModel,
                    context.CancellationToken
                );
                if (readOnlyAttr is not null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            IncompatibleModifierRule,
                            readOnlyAttr.GetLocation(),
                            "[ReadOnly] and [Mutable] cannot be applied to the same parameter"
                        )
                    );
                }

                return;
            }
            // [ReadOnly] incompatibilities
            case true:
            {
                var readOnlyAttr = FindReadOnlyAttributeSyntax(
                    parameter,
                    context.SemanticModel,
                    context.CancellationToken
                );
                if (readOnlyAttr is null)
                    return;

                // [ReadOnly] on ref/out/in
                var refKindToken = parameter.Modifiers.FirstOrDefault(m =>
                    m.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword
                );

                if (refKindToken.Kind() is not SyntaxKind.None)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            IncompatibleModifierRule,
                            readOnlyAttr.GetLocation(),
                            $"[ReadOnly] cannot be applied to a '{refKindToken.ValueText}' parameter"
                        )
                    );
                    return;
                }

                // [ReadOnly] on record or readonly struct primary constructor
                if (parameter.Parent?.Parent is not TypeDeclarationSyntax readOnlyTypeDecl)
                    return;

                var incompatibleKind = readOnlyTypeDecl switch
                {
                    RecordDeclarationSyntax { RawKind: (int)SyntaxKind.RecordDeclaration } => "record class",
                    RecordDeclarationSyntax => "record struct",
                    StructDeclarationSyntax when readOnlyTypeDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) =>
                        "readonly struct",
                    _ => null,
                };

                if (incompatibleKind is not null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            IncompatibleModifierRule,
                            readOnlyAttr.GetLocation(),
                            $"[ReadOnly] has no effect on a {incompatibleKind} parameter — it is already readonly"
                        )
                    );
                }

                return;
            }
        }

        // [Mutable] on record or readonly struct primary constructor
        if (hasMutable && parameter.Parent?.Parent is TypeDeclarationSyntax typeDecl)
        {
            var incompatibleKind = typeDecl switch
            {
                RecordDeclarationSyntax { RawKind: (int)SyntaxKind.RecordDeclaration } => "record class",
                RecordDeclarationSyntax => "record struct",
                StructDeclarationSyntax when typeDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) => "readonly struct",
                _ => null,
            };

            if (incompatibleKind is not null)
            {
                var mutableAttr = FindMutableAttributeSyntax(
                    parameter,
                    context.SemanticModel,
                    context.CancellationToken
                );
                if (mutableAttr is not null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            IncompatibleModifierRule,
                            mutableAttr.GetLocation(),
                            $"[Mutable] cannot be applied to a {incompatibleKind} parameter — record parameters generate properties, not fields"
                        )
                    );
                }
            }
        }
    }

    private static AttributeSyntax? FindMutableAttributeSyntax(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        foreach (var attrList in parameter.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrCtor = semanticModel.GetSymbolInfo(attr, cancellationToken).Symbol as IMethodSymbol;
                if (
                    attrCtor?.ContainingType is { } type
                    && string.Equals(type.Name, "MutableAttribute", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        type.ContainingNamespace?.ToDisplayString(),
                        "DemoriLabs.CodeAnalysis.Attributes",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return attr;
                }
            }
        }

        return null;
    }

    private static AttributeSyntax? FindReadOnlyAttributeSyntax(
        ParameterSyntax parameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        foreach (var attrList in parameter.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var attrCtor = semanticModel.GetSymbolInfo(attr, cancellationToken).Symbol as IMethodSymbol;
                if (
                    attrCtor?.ContainingType is { } type
                    && string.Equals(type.Name, "ReadOnlyAttribute", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        type.ContainingNamespace?.ToDisplayString(),
                        "DemoriLabs.CodeAnalysis.Attributes",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return attr;
                }
            }
        }

        return null;
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var target = context.Node switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand,
            _ => null!,
        };

        if (target is not IdentifierNameSyntax identifier)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;

        var parameter = ResolveParameter(symbol);

        if (parameter?.RefKind is not RefKind.None)
            return;

        if (!AnnotationAttributes.HasReadOnlyAttribute(parameter))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), parameter.Name));
    }

    private static IParameterSymbol? ResolveParameter(ISymbol? symbol)
    {
        return symbol switch
        {
            IParameterSymbol p => p,
            IFieldSymbol { IsImplicitlyDeclared: true } field => FindPrimaryConstructorParameter(
                field.ContainingType,
                field.Name
            ),
            IPropertySymbol property when IsPositionalRecordProperty(property) => FindPrimaryConstructorParameter(
                property.ContainingType,
                property.Name
            ),
            _ => null,
        };
    }

    private static bool IsPositionalRecordProperty(IPropertySymbol property)
    {
        return !property.DeclaringSyntaxReferences.Any(r => r.GetSyntax() is PropertyDeclarationSyntax);
    }

    private static IParameterSymbol? FindPrimaryConstructorParameter(INamedTypeSymbol? containingType, string name)
    {
        if (containingType is null)
            return null;

        foreach (var constructor in containingType.InstanceConstructors)
        {
            foreach (var syntaxRef in constructor.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();

                if (syntax is RecordDeclarationSyntax record)
                {
                    if (record.Kind() is SyntaxKind.RecordDeclaration)
                        return null;

                    if (record.Modifiers.Any(m => m.Kind() is SyntaxKind.ReadOnlyKeyword))
                        return null;
                }

                if (syntax is not TypeDeclarationSyntax)
                    continue;

                var match = constructor.Parameters.FirstOrDefault(p =>
                    string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                );

                if (match is not null)
                    return match;
            }
        }

        return null;
    }
}
