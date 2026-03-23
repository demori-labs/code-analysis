using System.Collections.Immutable;
using System.Threading;
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
    private const int DefaultNamedArgumentsThreshold = 2;

    private const string NamedArgumentsThresholdOptionKey = "dotnet_diagnostic.DL3001.named_arguments_threshold";

    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.NamedArgument,
        title: "Use named argument",
        messageFormat: "Use a named argument for parameter '{0}'",
        RuleCategories.Style,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Arguments should be named when the parameter is marked with [NamedArgument], or when the method has more parameters than the configured threshold."
    );

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var expressionType = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Linq.Expressions.Expression`1"
            );

            compilationContext.RegisterSyntaxNodeAction(
                analysisContext => AnalyzeArgument(analysisContext, expressionType),
                SyntaxKind.Argument
            );
        });
    }

    private static void AnalyzeArgument(SyntaxNodeAnalysisContext context, INamedTypeSymbol? expressionType)
    {
        var argument = (ArgumentSyntax)context.Node;

        if (argument.NameColon is not null)
            return;

        if (!argument.RefKindKeyword.IsKind(SyntaxKind.None))
            return;

        if (argument.Parent is BracketedArgumentListSyntax)
            return;

        if (
            expressionType is not null
            && IsInsideExpressionTree(argument, context.SemanticModel, expressionType, context.CancellationToken)
        )
        {
            return;
        }

        var operation = context.SemanticModel.GetOperation(argument, context.CancellationToken) as IArgumentOperation;
        if (operation?.Parameter is null)
            return;

        var parameter = operation.Parameter;

        if (parameter.IsParams)
            return;

        if (parameter.Type.TypeKind is TypeKind.Enum)
            return;

        // Rule 1: [NamedArgument] always reports
        if (AnnotationAttributes.HasNamedArgumentAttribute(parameter))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), parameter.Name));
            return;
        }

        // Rule 2: Methods exceeding the threshold require naming for non-matching arguments
        if (parameter.ContainingSymbol is IMethodSymbol method)
        {
            var visibleParameterCount = CountVisibleParameters(method);

            var namedArgumentsThreshold = GetNamedArgumentsThreshold(context);

            if (visibleParameterCount > namedArgumentsThreshold)
            {
                var argumentName = GetArgumentName(argument.Expression);
                if (argumentName is null || !NameMatches(argumentName, parameter))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, argument.GetLocation(), parameter.Name));
                }
            }
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

    private static bool NameMatches(string argumentName, IParameterSymbol parameter)
    {
        var parameterName = parameter.Name;

        if (string.Equals(argumentName, parameterName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (
            parameterName.StartsWith(argumentName, StringComparison.OrdinalIgnoreCase)
            && parameterName.Length > argumentName.Length
            && char.IsUpper(parameterName[argumentName.Length])
        )
        {
            var suffix = parameterName.Substring(argumentName.Length);
            var typeName = parameter.Type.Name;
            return string.Equals(suffix, typeName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static int CountVisibleParameters(IMethodSymbol method)
    {
        var count = 0;

        foreach (var param in method.Parameters)
        {
            if (param.IsParams)
                continue;

            if (param.RefKind is not RefKind.None)
                continue;

            if (param.HasExplicitDefaultValue)
                continue;

            count++;
        }

        if (method.IsExtensionMethod && count > 0)
            count--;

        return count;
    }

    private static int GetNamedArgumentsThreshold(SyntaxNodeAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);

        if (options.TryGetValue(NamedArgumentsThresholdOptionKey, out var value) && int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return DefaultNamedArgumentsThreshold;
    }

    private static bool IsInsideExpressionTree(
        SyntaxNode node,
        SemanticModel semanticModel,
        INamedTypeSymbol expressionType,
        CancellationToken cancellationToken
    )
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is not LambdaExpressionSyntax and not AnonymousMethodExpressionSyntax)
                continue;

            var typeInfo = semanticModel.GetTypeInfo(current, cancellationToken);
            var convertedType = typeInfo.ConvertedType?.OriginalDefinition;

            if (convertedType is not null && SymbolEqualityComparer.Default.Equals(convertedType, expressionType))
            {
                return true;
            }
        }

        return false;
    }
}
