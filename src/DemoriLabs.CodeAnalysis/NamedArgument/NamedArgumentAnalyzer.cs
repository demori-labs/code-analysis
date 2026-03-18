using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DemoriLabs.CodeAnalysis.NamedArgument;

/// <inheritdoc />
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamedArgumentAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.NamedArgument,
        title: "Use named argument",
        messageFormat: "Use a named argument for parameter '{0}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Arguments should be named when passed as ambiguous literals, when the variable name does not match the parameter name, or when the parameter is marked with [NamedArgument]."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeArgument, SyntaxKind.Argument);
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;

        if (argument.NameColon is not null)
            return;

        if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
            return;

        if (argument.Parent is BracketedArgumentListSyntax)
            return;

        var operation = context.SemanticModel.GetOperation(argument, context.CancellationToken) as IArgumentOperation;
        if (operation?.Parameter is null)
            return;

        var parameter = operation.Parameter;

        if (parameter.IsParams)
            return;

        if (AnnotationAttributes.HasNamedArgumentAttribute(parameter) || IsAmbiguousLiteral(argument.Expression))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), parameter.Name));
            return;
        }

        var argumentName = GetArgumentName(argument.Expression);
        if (argumentName is null || !string.Equals(argumentName, parameter.Name, StringComparison.OrdinalIgnoreCase))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), parameter.Name));
        }
    }

    private static string? GetArgumentName(ExpressionSyntax expression)
    {
        var name = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null,
        };

        return name?.TrimStart('_');
    }

    private static bool IsAmbiguousLiteral(ExpressionSyntax expression)
    {
        return expression.Kind()
            is SyntaxKind.NullLiteralExpression
                or SyntaxKind.TrueLiteralExpression
                or SyntaxKind.FalseLiteralExpression
                or SyntaxKind.NumericLiteralExpression
                or SyntaxKind.StringLiteralExpression
                or SyntaxKind.CharacterLiteralExpression
                or SyntaxKind.DefaultLiteralExpression;
    }
}
