using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.CodeFixes;

internal static class IndentationResolver
{
    private const string DefaultIndent = "    ";

    internal static string GetIndentUnit(Document document, SyntaxTree syntaxTree)
    {
        var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

        var useTabs =
            options.TryGetValue("indent_style", out var style)
            && string.Equals(style, "tab", StringComparison.OrdinalIgnoreCase);

        if (useTabs)
        {
            return "\t";
        }

        if (options.TryGetValue("indent_size", out var sizeValue) && int.TryParse(sizeValue, out var size))
        {
            return new string(' ', size);
        }

        return DefaultIndent;
    }
}
