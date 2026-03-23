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
        description: "Parameters marked with [ReadOnly] cannot be reassigned within the method body. "
            + "Introduce a new local variable instead."
    );

    private static readonly DiagnosticDescriptor IncompatibleModifierRule = new(
        RuleIdentifiers.ReadOnlyIncompatibleModifier,
        title: "[ReadOnly] is incompatible with this parameter modifier",
        messageFormat: "[ReadOnly] cannot be applied to a '{0}' parameter",
        RuleCategories.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "'out' parameters must be assigned by the callee, 'ref' parameters exist to be mutated, "
            + "and 'in' parameters are already read-only by the modifier. "
            + "Remove [ReadOnly] or change the parameter modifier."
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

        var readOnlyAttr = FindReadOnlyAttributeSyntax(parameter, context.SemanticModel, context.CancellationToken);
        if (readOnlyAttr is null)
            return;

        var refKindToken = parameter.Modifiers.FirstOrDefault(m =>
            m.Kind() is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword or SyntaxKind.InKeyword
        );

        if (refKindToken.Kind() is not SyntaxKind.None)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(IncompatibleModifierRule, readOnlyAttr.GetLocation(), refKindToken.ValueText)
            );
            return;
        }

        if (
            parameter.Parent?.Parent is not RecordDeclarationSyntax record
            || (
                record.Kind() is not SyntaxKind.RecordDeclaration
                && !record.Modifiers.Any(m => m.Kind() is SyntaxKind.ReadOnlyKeyword)
            )
        )
        {
            return;
        }

        var kind = record.Kind() is SyntaxKind.RecordDeclaration ? "record class" : "readonly record struct";
        context.ReportDiagnostic(Diagnostic.Create(IncompatibleModifierRule, readOnlyAttr.GetLocation(), kind));
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
        if (parameter is null)
            return;

        if (parameter.RefKind is not RefKind.None)
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
