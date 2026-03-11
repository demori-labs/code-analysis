using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.Diagnostics.InvertIf;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InvertIfToReduceNestingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.InvertIfToReduceNesting,
        title: "Invert 'if' statement to reduce nesting",
        messageFormat: "Invert 'if' statement to reduce nesting",
        RuleCategories.Style,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "An 'if' statement that wraps the remaining body of a method can be inverted to an early return, reducing nesting depth."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
    }

    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        if (ifStatement.Else is not null)
            return;

        if (ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 } block)
            return;

        if (ifStatement.Parent is not BlockSyntax parentBlock)
            return;

        var ifIndex = parentBlock.Statements.IndexOf(ifStatement);
        var isLast = ifIndex == parentBlock.Statements.Count - 1;
        var isSecondToLast = ifIndex == parentBlock.Statements.Count - 2;

        if (isLast)
        {
            if (!IsVoidReturningContext(parentBlock, context.SemanticModel, context.CancellationToken))
                return;
        }
        else if (isSecondToLast
            && parentBlock.Statements[ifIndex + 1] is ReturnStatementSyntax
            && block.Statements.Count >= 2)
        {
            // If followed by a return and body has multiple statements — worth inverting
        }
        else
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.IfKeyword.GetLocation()));
    }

    private static bool IsVoidReturningContext(BlockSyntax block, SemanticModel semanticModel, CancellationToken ct)
    {
        return block.Parent switch
        {
            ConstructorDeclarationSyntax => true,
            DestructorDeclarationSyntax => true,
            AccessorDeclarationSyntax => true,
            MethodDeclarationSyntax method => IsVoidOrTaskReturning(method, semanticModel, ct),
            LocalFunctionStatementSyntax localFunc => IsVoidOrTaskReturning(localFunc, semanticModel, ct),
            _ => false,
        };
    }

    private static bool IsVoidOrTaskReturning(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var symbol = semanticModel.GetDeclaredSymbol(method, ct);
        return symbol is not null && IsVoidOrTaskReturnType(symbol.ReturnType);
    }

    private static bool IsVoidOrTaskReturning(
        LocalFunctionStatementSyntax localFunc,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        var symbol = semanticModel.GetDeclaredSymbol(localFunc, ct) as IMethodSymbol;
        return symbol is not null && IsVoidOrTaskReturnType(symbol.ReturnType);
    }

    private static bool IsVoidOrTaskReturnType(ITypeSymbol returnType)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
            return true;

        if (returnType is INamedTypeSymbol { IsGenericType: false } namedType
            && namedType.Name is "Task" or "ValueTask"
            && namedType.ContainingNamespace is
            {
                Name: "Tasks",
                ContainingNamespace:
                {
                    Name: "Threading",
                    ContainingNamespace:
                    {
                        Name: "System",
                        ContainingNamespace.IsGlobalNamespace: true,
                    },
                },
            })
        {
            return true;
        }

        return false;
    }
}
