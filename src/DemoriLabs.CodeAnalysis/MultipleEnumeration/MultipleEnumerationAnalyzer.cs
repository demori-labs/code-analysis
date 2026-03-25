using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DemoriLabs.CodeAnalysis.MultipleEnumeration;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleEnumerationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: RuleIdentifiers.PossibleMultipleEnumeration,
        title: "Possible multiple enumeration",
        messageFormat: "Possible multiple enumeration of '{0}'",
        category: RuleCategories.Performance,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Enumerating an IEnumerable multiple times may cause performance issues or unexpected behavior. Consider materializing the sequence with ToList() or ToArray()."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            AnalyzeBody,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.LocalFunctionStatement
        );
    }

    private static void AnalyzeBody(SyntaxNodeAnalysisContext context)
    {
        SeparatedSyntaxList<ParameterSyntax> parameterSyntax;
        SyntaxNode? body;

        switch (context.Node)
        {
            case MethodDeclarationSyntax method:
                parameterSyntax = method.ParameterList.Parameters;
                body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                break;

            case ConstructorDeclarationSyntax ctor:
                parameterSyntax = ctor.ParameterList.Parameters;
                body = (SyntaxNode?)ctor.Body ?? ctor.ExpressionBody;
                break;

            case LocalFunctionStatementSyntax localFunc:
                parameterSyntax = localFunc.ParameterList.Parameters;
                body = (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody;
                break;

            default:
                return;
        }

        if (body is null)
            return;

        if (MayHaveIEnumerableCandidates(parameterSyntax, body) is false)
            return;

        var bodyOperation = context.SemanticModel.GetOperation(body, context.CancellationToken);
        if (bodyOperation is null)
            return;

        if (
            context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken)
            is not IMethodSymbol methodSymbol
        )
        {
            return;
        }

        var candidates = CollectParameterCandidates(methodSymbol.Parameters);
        var sites = new Dictionary<ISymbol, List<Location>>(SymbolEqualityComparer.Default);

        WalkOperations(bodyOperation, candidates, sites);

        foreach (var kvp in sites)
        {
            if (kvp.Value.Count <= 1)
                continue;

            foreach (var location in kvp.Value)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, kvp.Key.Name));
            }
        }
    }

    private static bool MayHaveIEnumerableCandidates(
        SeparatedSyntaxList<ParameterSyntax> parameters,
        SyntaxNode body
    )
    {
        foreach (var param in parameters)
        {
            if (TypeSyntaxMayBeIEnumerable(param.Type))
                return true;
        }

        foreach (var node in body.DescendantNodes())
        {
            if (node is VariableDeclarationSyntax varDecl)
            {
                if (TypeSyntaxMayBeIEnumerable(varDecl.Type))
                    return true;

                if (varDecl.Type is IdentifierNameSyntax { Identifier.ValueText: "var" })
                    return true;
            }
        }

        return false;
    }

    private static bool TypeSyntaxMayBeIEnumerable(TypeSyntax? type)
    {
        return type switch
        {
            GenericNameSyntax gen => gen.Identifier.ValueText is "IEnumerable",
            IdentifierNameSyntax id => id.Identifier.ValueText is "IEnumerable",
            QualifiedNameSyntax qual => TypeSyntaxMayBeIEnumerable(qual.Right),
            NullableTypeSyntax nullable => TypeSyntaxMayBeIEnumerable(nullable.ElementType),
            _ => false,
        };
    }

    private static HashSet<ISymbol> CollectParameterCandidates(ImmutableArray<IParameterSymbol> parameters)
    {
        var candidates = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var param in parameters)
        {
            if (IsIEnumerableType(param.Type) is false)
                continue;

            if (AnnotationAttributes.HasSuppressMultipleEnumerationAttribute(param))
                continue;

            candidates.Add(param);
        }

        return candidates;
    }

    private static void WalkOperations(
        IOperation root,
        HashSet<ISymbol> candidates,
        Dictionary<ISymbol, List<Location>> sites
    )
    {
        foreach (var operation in root.Descendants())
        {
            switch (operation)
            {
                case IAnonymousFunctionOperation:
                case ILocalFunctionOperation:
                    continue;

                case IVariableDeclaratorOperation declarator
                    when declarator.Symbol is ILocalSymbol local
                        && IsIEnumerableType(local.Type)
                        && IsInNestedScope(declarator, root) is false:
                    candidates.Add(local);
                    break;

                case IForEachLoopOperation loop when IsInNestedScope(loop, root) is false:
                    RecordIfCandidate(loop.Collection, candidates, sites);
                    break;

                case IInvocationOperation invocation
                    when IsEnumeratingMethod(invocation.TargetMethod)
                        && IsInNestedScope(invocation, root) is false:
                    var target = invocation.TargetMethod.IsExtensionMethod
                        ? invocation.Arguments.Length > 0
                            ? invocation.Arguments[0].Value
                            : null
                        : invocation.Instance;

                    if (target is not null)
                    {
                        RecordIfCandidate(target, candidates, sites);
                    }

                    break;
            }
        }
    }

    private static bool IsIEnumerableType(ITypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T
            || type.SpecialType is SpecialType.System_Collections_IEnumerable;
    }

    private static bool IsInNestedScope(IOperation operation, IOperation root)
    {
        var parent = operation.Parent;
        while (parent is not null && parent != root)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
                return true;

            parent = parent.Parent;
        }

        return false;
    }

    private static void RecordIfCandidate(
        IOperation operation,
        HashSet<ISymbol> candidates,
        Dictionary<ISymbol, List<Location>> sites
    )
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        ISymbol? symbol = operation switch
        {
            IParameterReferenceOperation paramRef => paramRef.Parameter,
            ILocalReferenceOperation localRef => localRef.Local,
            _ => null,
        };

        if (symbol is null || candidates.Contains(symbol) is false)
            return;

        if (sites.TryGetValue(symbol, out var list) is false)
        {
            list = [];
            sites[symbol] = list;
        }

        list.Add(operation.Syntax.GetLocation());
    }

    private static bool IsEnumeratingMethod(IMethodSymbol method)
    {
        if (string.Equals(method.Name, "GetEnumerator", StringComparison.Ordinal))
            return true;

        var containingType = method.ContainingType;
        if (containingType is null)
            return false;

        var ns = containingType.ContainingNamespace?.ToDisplayString();
        return string.Equals(ns, "System.Linq", StringComparison.Ordinal)
            && (
                string.Equals(containingType.Name, "Enumerable", StringComparison.Ordinal)
                || string.Equals(containingType.Name, "ParallelEnumerable", StringComparison.Ordinal)
            );
    }
}
