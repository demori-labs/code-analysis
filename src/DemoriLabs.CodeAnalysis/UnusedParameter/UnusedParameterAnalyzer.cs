using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DemoriLabs.CodeAnalysis.UnusedParameter;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedParameterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UnusedParameter,
        title: "Unused method parameter",
        messageFormat: "Parameter '{0}' is never used",
        RuleCategories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Method parameters that are never referenced in the method body can be safely removed to reduce complexity and improve readability."
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
            SyntaxKind.LocalFunctionStatement
        );
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var (parameters, body) = context.Node switch
        {
            MethodDeclarationSyntax m => (m.ParameterList.Parameters, (SyntaxNode?)m.Body ?? m.ExpressionBody),
            ConstructorDeclarationSyntax c => (c.ParameterList.Parameters, (SyntaxNode?)c.Body ?? c.ExpressionBody),
            LocalFunctionStatementSyntax l => (l.ParameterList.Parameters, (SyntaxNode?)l.Body ?? l.ExpressionBody),
            _ => (default, null),
        };

        if (body is null || parameters.Count == 0)
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

        var candidates = CollectCandidates(methodSymbol);
        if (candidates.Count == 0)
            return;

        var bodyOperation = context.SemanticModel.GetOperation(body, context.CancellationToken);
        if (bodyOperation is null)
            return;

        foreach (var operation in bodyOperation.Descendants())
        {
            if (operation is IParameterReferenceOperation paramRef)
            {
                candidates.Remove(paramRef.Parameter);
                if (candidates.Count == 0)
                    return;
            }
        }

        foreach (var param in candidates)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, param.Locations[0], param.Name));
        }
    }

    private static bool ShouldSkipMethod(IMethodSymbol method)
    {
        if (method.IsOverride || method.IsVirtual || method.IsAbstract || method.IsExtern)
            return true;

        if (method.MethodKind is MethodKind.ExplicitInterfaceImplementation)
            return true;

        if (IsImplicitInterfaceImplementation(method))
            return true;

        if (IsEntryPoint(method))
            return true;

        if (IsEventHandlerSignature(method))
            return true;

        return false;
    }

    private static bool IsImplicitInterfaceImplementation(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null || containingType.AllInterfaces.Length == 0)
            return false;

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is not IMethodSymbol)
                    continue;

                var impl = containingType.FindImplementationForInterfaceMember(member);
                if (SymbolEqualityComparer.Default.Equals(impl, method))
                    return true;
            }
        }

        return false;
    }

    private static bool IsEntryPoint(IMethodSymbol method)
    {
        return method.IsStatic && string.Equals(method.Name, "Main", System.StringComparison.Ordinal);
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
                string.Equals(current.Name, "EventArgs", System.StringComparison.Ordinal)
                && string.Equals(
                    current.ContainingNamespace?.ToDisplayString(),
                    "System",
                    System.StringComparison.Ordinal
                )
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

        var startIndex = method.IsExtensionMethod ? 1 : 0;

        for (var i = startIndex; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];

            if (param.RefKind is RefKind.Out)
                continue;

            if (param.Name.Length > 0 && param.Name[0] == '_')
                continue;

            candidates.Add(param);
        }

        return candidates;
    }
}
