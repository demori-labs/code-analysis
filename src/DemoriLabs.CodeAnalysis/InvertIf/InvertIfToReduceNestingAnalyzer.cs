using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.InvertIf;

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

        if (ifStatement.Statement is not BlockSyntax { Statements.Count: > 0 })
            return;

        // Don't flag if this if is part of an else-if chain (the outer if will handle it)
        if (ifStatement.Parent?.Parent is ElseClauseSyntax)
            return;

        SyntaxList<StatementSyntax> parentStatements;
        ExitContext exitContext;

        switch (ifStatement.Parent)
        {
            case BlockSyntax parentBlock:
                parentStatements = parentBlock.Statements;
                exitContext = GetExitContext(parentBlock, context.SemanticModel, context.CancellationToken);
                break;
            case SwitchSectionSyntax switchSection:
                parentStatements = switchSection.Statements;
                exitContext = ExitContext.ValueReturn;
                break;
            default:
                return;
        }

        if (exitContext is ExitContext.None)
            return;

        var ifIndex = parentStatements.IndexOf(ifStatement);
        var isLast = ifIndex == parentStatements.Count - 1;
        var isSecondToLast = ifIndex == parentStatements.Count - 2;

        if (ifStatement.Else is not null)
        {
            if (!ElseChainEndsWithExits(ifStatement.Else))
            {
                return;
            }
        }
        else if (isLast)
        {
            if (exitContext is not (ExitContext.VoidReturn or ExitContext.Continue))
            {
                return;
            }
        }
        else if (isSecondToLast is false || IsExitStatement(parentStatements[ifIndex + 1]) is false)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, ifStatement.IfKeyword.GetLocation()));
    }

    private static bool ElseChainEndsWithExits(ElseClauseSyntax elseClause)
    {
        var elseStatement = elseClause.Statement;

        return elseStatement switch
        {
            BlockSyntax block => block.Statements.Count > 0 && IsExitStatement(block.Statements.Last()),
            IfStatementSyntax { Statement: BlockSyntax elseIfBlock }
                when IsExitStatement(elseIfBlock.Statements[elseIfBlock.Statements.Count - 1]) is false => false,
            IfStatementSyntax { Else: not null } elseIf => ElseChainEndsWithExits(elseIf.Else),
            IfStatementSyntax => true,
            _ => IsExitStatement(elseStatement),
        };
    }

    private static bool IsExitStatement(StatementSyntax statement)
    {
        return statement
                is ReturnStatementSyntax
                    or ThrowStatementSyntax
                    or BreakStatementSyntax
                    or ContinueStatementSyntax
            || statement.IsKind(SyntaxKind.YieldBreakStatement);
    }

    private static ExitContext GetExitContext(BlockSyntax block, SemanticModel semanticModel, CancellationToken ct)
    {
        return block.Parent switch
        {
            MethodDeclarationSyntax method => GetMethodExitContext(method, semanticModel, ct),
            ForEachStatementSyntax => ExitContext.Continue,
            ForStatementSyntax => ExitContext.Continue,
            WhileStatementSyntax => ExitContext.Continue,
            DoStatementSyntax => ExitContext.Continue,
            ConstructorDeclarationSyntax => ExitContext.VoidReturn,
            DestructorDeclarationSyntax => ExitContext.VoidReturn,
            AccessorDeclarationSyntax => ExitContext.VoidReturn,
            LocalFunctionStatementSyntax localFunc => GetLocalFunctionExitContext(localFunc, semanticModel, ct),
            SimpleLambdaExpressionSyntax => ExitContext.VoidReturn,
            ParenthesizedLambdaExpressionSyntax => ExitContext.VoidReturn,
            AnonymousMethodExpressionSyntax => ExitContext.VoidReturn,
            SwitchSectionSyntax => ExitContext.ValueReturn,
            _ => ExitContext.None,
        };
    }

    private static ExitContext GetMethodExitContext(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        // Fast syntactic check: void and other predefined types (int, bool, etc.)
        if (method.ReturnType is PredefinedTypeSyntax predefined)
        {
            return predefined.Keyword.IsKind(SyntaxKind.VoidKeyword) ? ExitContext.VoidReturn : ExitContext.ValueReturn;
        }

        // Fast syntactic check: types that can never be non-generic Task/ValueTask
        if (
            method.ReturnType
            is GenericNameSyntax
                or ArrayTypeSyntax
                or TupleTypeSyntax
                or NullableTypeSyntax
                or PointerTypeSyntax
                or RefTypeSyntax
        )
        {
            return ExitContext.ValueReturn;
        }

        // Slow path: could be Task, ValueTask, or other — need semantic model
        var symbol = semanticModel.GetDeclaredSymbol(method, ct);
        if (symbol is null)
            return ExitContext.None;

        return IsVoidOrTaskReturnType(symbol.ReturnType) ? ExitContext.VoidReturn : ExitContext.ValueReturn;
    }

    private static ExitContext GetLocalFunctionExitContext(
        LocalFunctionStatementSyntax localFunc,
        SemanticModel semanticModel,
        CancellationToken ct
    )
    {
        // Fast syntactic check: void and other predefined types
        if (localFunc.ReturnType is PredefinedTypeSyntax predefined)
        {
            return predefined.Keyword.IsKind(SyntaxKind.VoidKeyword) ? ExitContext.VoidReturn : ExitContext.ValueReturn;
        }

        // Fast syntactic check: types that can never be non-generic Task/ValueTask
        if (
            localFunc.ReturnType
            is GenericNameSyntax
                or ArrayTypeSyntax
                or TupleTypeSyntax
                or NullableTypeSyntax
                or PointerTypeSyntax
                or RefTypeSyntax
        )
        {
            return ExitContext.ValueReturn;
        }

        // Slow path: could be Task, ValueTask, or other — need semantic model
        if (semanticModel.GetDeclaredSymbol(localFunc, ct) is not { } symbol)
            return ExitContext.None;

        return IsVoidOrTaskReturnType(symbol.ReturnType) ? ExitContext.VoidReturn : ExitContext.ValueReturn;
    }

    private static bool IsVoidOrTaskReturnType(ITypeSymbol returnType)
    {
        if (returnType.SpecialType is SpecialType.System_Void)
            return true;

        return returnType
            is INamedTypeSymbol
            {
                IsGenericType: false,
                Name: "Task" or "ValueTask",
                ContainingNamespace:
                {
                    Name: "Tasks",
                    ContainingNamespace:
                    {
                        Name: "Threading",
                        ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true },
                    },
                },
            };
    }
}
