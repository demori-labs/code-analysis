using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.PrimaryConstructor;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsePrimaryConstructorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.UsePrimaryConstructor,
        title: "Type can use a primary constructor",
        messageFormat: "Constructor of '{0}' can be converted to a primary constructor",
        RuleCategories.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Types with a single constructor that only assigns parameters to readonly fields can use a primary constructor for more concise syntax."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(static ctx => AnalyzeConstructor(ctx), SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructorSyntax = (ConstructorDeclarationSyntax)context.Node;

        if (constructorSyntax.Body is not { } body)
            return;

        if (body.Statements.Count == 0 && constructorSyntax.Initializer is null)
            return;

        if (constructorSyntax.Parent is not TypeDeclarationSyntax typeDecl)
            return;

        if (typeDecl is not (ClassDeclarationSyntax or StructDeclarationSyntax))
            return;

        if (typeDecl.ParameterList is not null)
            return;

        if (typeDecl.Modifiers.Any(static m => m.Kind() is SyntaxKind.PartialKeyword or SyntaxKind.StaticKeyword))
        {
            return;
        }

        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, context.CancellationToken);
        if (typeSymbol is null)
            return;

        if (typeSymbol.IsRecord)
            return;

        if (AnnotationAttributes.HasMutableAttribute(typeSymbol))
            return;

        var explicitConstructors = typeSymbol.Constructors.Where(static c =>
            c is { IsImplicitlyDeclared: false, IsStatic: false }
        );

        var constructorCount = 0;
        foreach (var _ in explicitConstructors)
        {
            constructorCount++;
            if (constructorCount > 1)
                return;
        }

        if (constructorCount is not 1)
            return;

        var constructorSymbol = context.SemanticModel.GetDeclaredSymbol(constructorSyntax, context.CancellationToken);
        if (constructorSymbol is null || constructorSymbol.Parameters.Length is 0)
            return;

        if (IsValidFieldAssignmentConstructor(body, constructorSyntax, constructorSymbol, typeSymbol, context) is false)
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, constructorSyntax.Identifier.GetLocation(), typeSymbol.Name));
    }

    private static bool IsValidFieldAssignmentConstructor(
        BlockSyntax body,
        ConstructorDeclarationSyntax constructorSyntax,
        IMethodSymbol constructor,
        INamedTypeSymbol containingType,
        SyntaxNodeAnalysisContext context
    )
    {
        var parameterSymbols = constructor.Parameters;
        var accountedParameters = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        var isClass = containingType.TypeKind is TypeKind.Class;
        var semanticModel = context.SemanticModel;

        // Account for parameters passed through in base/this initializer
        if (constructorSyntax.Initializer is { ArgumentList.Arguments: { Count: > 0 } initializerArgs })
        {
            foreach (var arg in initializerArgs)
            {
                var argSymbol = semanticModel.GetSymbolInfo(arg.Expression, context.CancellationToken).Symbol;
                if (
                    argSymbol is IParameterSymbol initParam
                    && parameterSymbols.Contains(initParam, SymbolEqualityComparer.Default)
                )
                {
                    accountedParameters.Add(initParam);
                }
            }
        }

        foreach (var statement in body.Statements)
        {
            if (
                statement
                is not ExpressionStatementSyntax
                {
                    Expression: AssignmentExpressionSyntax
                    {
                        RawKind: (int)SyntaxKind.SimpleAssignmentExpression
                    } assignment
                }
            )
            {
                return false;
            }

            var leftSymbol = ResolveMemberSymbol(assignment.Left, semanticModel, context.CancellationToken);
            if (leftSymbol is null)
                return false;

            if (SymbolEqualityComparer.Default.Equals(leftSymbol.ContainingType, containingType) is false)
                return false;

            if (isClass && IsReadOnlyMember(leftSymbol) is false)
                return false;

            var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right, context.CancellationToken).Symbol;
            if (rightSymbol is not IParameterSymbol paramSymbol)
                return false;

            if (parameterSymbols.Contains(paramSymbol, SymbolEqualityComparer.Default) is false)
                return false;

            accountedParameters.Add(paramSymbol);
        }

        return accountedParameters.Count == parameterSymbols.Length;
    }

    private static bool IsReadOnlyMember(ISymbol symbol)
    {
        return symbol switch
        {
            IFieldSymbol field => field.IsReadOnly,
            IPropertySymbol property => property.SetMethod is null or { IsInitOnly: true },
            _ => false,
        };
    }

    private static ISymbol? ResolveMemberSymbol(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken
    )
    {
        var target = expression switch
        {
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } memberAccess => memberAccess,
            IdentifierNameSyntax => expression,
            _ => null,
        };

        if (target is null)
            return null;

        var symbol = semanticModel.GetSymbolInfo(target, cancellationToken).Symbol;
        return symbol is IFieldSymbol or IPropertySymbol ? symbol : null;
    }
}
