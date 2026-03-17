using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.MultipleEnumeration;

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
        SyntaxNode? body;
        ImmutableArray<IParameterSymbol> parameters;

        switch (context.Node)
        {
            case MethodDeclarationSyntax method:
                body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
                var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken);
                if (methodSymbol is null)
                    return;
                parameters = methodSymbol.Parameters;
                break;

            case ConstructorDeclarationSyntax ctor:
                body = (SyntaxNode?)ctor.Body ?? ctor.ExpressionBody;
                var ctorSymbol = context.SemanticModel.GetDeclaredSymbol(ctor, context.CancellationToken);
                if (ctorSymbol is null)
                    return;
                parameters = ctorSymbol.Parameters;
                break;

            case LocalFunctionStatementSyntax localFunc:
                body = (SyntaxNode?)localFunc.Body ?? localFunc.ExpressionBody;
                var localFuncSymbol = context.SemanticModel.GetDeclaredSymbol(localFunc, context.CancellationToken);
                if (localFuncSymbol is null)
                    return;
                parameters = localFuncSymbol.Parameters;
                break;

            default:
                return;
        }

        if (body is null)
            return;

        var candidates = CollectCandidates(parameters, body, context.SemanticModel, context.CancellationToken);
        if (candidates.Count is 0)
            return;

        var finder = new EnumerationFinder(candidates, context.SemanticModel, context.CancellationToken);
        finder.Visit(body);

        foreach (var kvp in finder.Sites)
        {
            if (kvp.Value.Count <= 1)
                continue;

            foreach (var location in kvp.Value)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, kvp.Key.Name));
            }
        }
    }

    private static HashSet<ISymbol> CollectCandidates(
        ImmutableArray<IParameterSymbol> parameters,
        SyntaxNode body,
        SemanticModel model,
        CancellationToken ct
    )
    {
        var candidates = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (
            var param in parameters.Where(p =>
                IsIEnumerableType(p.Type) && !AnnotationAttributes.HasSuppressMultipleEnumerationAttribute(p)
            )
        )
        {
            candidates.Add(param);
        }

        foreach (var declarator in body.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            var symbol = model.GetDeclaredSymbol(declarator, ct);
            if (symbol is ILocalSymbol local && IsIEnumerableType(local.Type))
                candidates.Add(local);
        }

        return candidates;
    }

    private static bool IsIEnumerableType(ITypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T
            || type.SpecialType is SpecialType.System_Collections_IEnumerable;
    }

    private sealed class EnumerationFinder(HashSet<ISymbol> candidates, SemanticModel model, CancellationToken ct)
        : CSharpSyntaxWalker
    {
        private readonly Dictionary<ISymbol, List<Location>> _sites = new(SymbolEqualityComparer.Default);

        public IReadOnlyDictionary<ISymbol, List<Location>> Sites => _sites;

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            RecordIfCandidate(node.Expression);
            base.VisitForEachStatement(node);
        }

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            RecordIfCandidate(node.Expression);
            base.VisitForEachVariableStatement(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (
                node.Expression is MemberAccessExpressionSyntax memberAccess
                && model.GetSymbolInfo(node, ct).Symbol is IMethodSymbol method
                && IsEnumeratingMethod(method)
            )
            {
                RecordIfCandidate(memberAccess.Expression);
            }

            base.VisitInvocationExpression(node);
        }

        public override void VisitFromClause(FromClauseSyntax node)
        {
            RecordIfCandidate(node.Expression);
            base.VisitFromClause(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            // Prevents walking into nested scope
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            // Prevents walking into nested scope
        }

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
        {
            // Prevents walking into nested scope
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            // Prevents walking into nested scope
        }

        private void RecordIfCandidate(ExpressionSyntax expression)
        {
            var symbol = model.GetSymbolInfo(expression, ct).Symbol;

            if (symbol is null || !candidates.Contains(symbol))
                return;

            if (!_sites.TryGetValue(symbol, out var list))
            {
                list = [];
                _sites[symbol] = list;
            }

            list.Add(expression.GetLocation());
        }

        private static bool IsEnumeratingMethod(IMethodSymbol method)
        {
            if (string.Equals(method.Name, "GetEnumerator", StringComparison.OrdinalIgnoreCase))
                return true;

            var containingType = method.ContainingType;
            if (containingType is null)
                return false;

            var ns = containingType.ContainingNamespace?.ToDisplayString();
            return string.Equals(ns, "System.Linq", StringComparison.OrdinalIgnoreCase)
                && (
                    string.Equals(containingType.Name, "Enumerable", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(containingType.Name, "ParallelEnumerable", StringComparison.OrdinalIgnoreCase)
                );
        }
    }
}
