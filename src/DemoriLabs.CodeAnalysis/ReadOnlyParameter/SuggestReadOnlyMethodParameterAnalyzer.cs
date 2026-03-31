using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.ReadOnlyParameter;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuggestReadOnlyMethodParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.SuggestReadOnlyMethodParameter,
        title: "Method parameter should be [ReadOnly]",
        messageFormat: "Consider adding [ReadOnly] to parameter '{0}'",
        RuleCategories.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Method parameters that are never reassigned should be marked with [ReadOnly] to prevent accidental mutation."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            static ctx => AnalyzeMethod(ctx),
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.ExtensionBlockDeclaration
        );
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        // Extension block receiver parameters
        if (context.Node is TypeDeclarationSyntax { RawKind: (int)SyntaxKind.ExtensionBlockDeclaration } extensionBlock)
        {
            AnalyzeExtensionBlockReceiver(context, extensionBlock);
            return;
        }

        var parameters = context.Node switch
        {
            MethodDeclarationSyntax { Body: not null } or MethodDeclarationSyntax { ExpressionBody: not null } => (
                (MethodDeclarationSyntax)context.Node
            )
                .ParameterList
                .Parameters,
            ConstructorDeclarationSyntax { Body: not null }
            or ConstructorDeclarationSyntax { ExpressionBody: not null } => ((ConstructorDeclarationSyntax)context.Node)
                .ParameterList
                .Parameters,
            LocalFunctionStatementSyntax { Body: not null }
            or LocalFunctionStatementSyntax { ExpressionBody: not null } => ((LocalFunctionStatementSyntax)context.Node)
                .ParameterList
                .Parameters,
            _ => default,
        };

        if (parameters.Count == 0)
            return;

        if (
            context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol methodSymbol
        )
        {
            return;
        }

        if (ShouldSkipMethod(methodSymbol))
            return;

        foreach (var param in CollectCandidates(methodSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, param.Locations[0], param.Name));
        }
    }

    private static void AnalyzeExtensionBlockReceiver(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax extensionBlock
    )
    {
        var parameterList = extensionBlock.ChildNodes().OfType<ParameterListSyntax>().FirstOrDefault();
        if (parameterList is null)
            return;

        foreach (var parameter in parameterList.Parameters)
        {
            // Unnamed receiver (static extension) — skip
            if (parameter.Identifier.IsMissing || parameter.Identifier.Text.Length == 0)
                continue;

            var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken);
            if (parameterSymbol is null)
                continue;

            if (AnnotationAttributes.HasReadOnlyAttribute(parameterSymbol))
                continue;

            if (AnnotationAttributes.HasMutableAttribute(parameterSymbol))
                continue;

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, parameter.Identifier.GetLocation(), parameter.Identifier.Text)
            );
        }
    }

    private static bool ShouldSkipMethod(IMethodSymbol method)
    {
        if (method.IsAbstract || method.IsExtern)
            return true;

        if (method.MethodKind is MethodKind.ExplicitInterfaceImplementation)
            return true;

        if (IsEntryPoint(method))
            return true;

        if (IsEventHandlerSignature(method))
            return true;

        return false;
    }

    private static bool IsEntryPoint(IMethodSymbol method)
    {
        return method.IsStatic && string.Equals(method.Name, "Main", StringComparison.Ordinal);
    }

    private static bool IsEventHandlerSignature(IMethodSymbol method)
    {
        if (method.Parameters.Length is not 2)
            return false;

        var first = method.Parameters[0];
        var second = method.Parameters[1];

        return first.Type.SpecialType is SpecialType.System_Object && InheritsFromEventArgs(second.Type);
    }

    private static bool InheritsFromEventArgs(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (
                string.Equals(current.Name, "EventArgs", StringComparison.Ordinal)
                && string.Equals(current.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal)
            )
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static HashSet<ISymbol> CollectCandidates(IMethodSymbol method)
    {
        var candidates = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        var startIndex = 0;

        for (var i = startIndex; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];

            if (param.RefKind is not RefKind.None)
                continue;

            if (param.Name.Length > 0 && param.Name[0] == '_')
                continue;

            if (AnnotationAttributes.HasReadOnlyAttribute(param))
                continue;

            if (AnnotationAttributes.HasMutableAttribute(param))
                continue;

            candidates.Add(param);
        }

        return candidates;
    }
}
