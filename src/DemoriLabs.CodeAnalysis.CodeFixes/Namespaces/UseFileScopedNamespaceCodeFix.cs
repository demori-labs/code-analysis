using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.Namespaces;

/// <inheritdoc />
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class UseFileScopedNamespaceCodeFix : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.UseFileScopedNamespace];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics[0];
        if (
            diagnostic.Properties.TryGetValue("FlattenedNamespace", out var flattenedNamespace) is false
            || flattenedNamespace is null
        )
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to file-scoped namespace",
                ct => ConvertToFileScopedAsync(context.Document, flattenedNamespace, ct),
                equivalenceKey: nameof(UseFileScopedNamespaceCodeFix)
            ),
            diagnostic
        );
    }

    private static async Task<Document> ConvertToFileScopedAsync(
        Document document,
        string flattenedNamespace,
        CancellationToken ct
    )
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
            return document;

        var blockNamespaces = compilationUnit.Members.OfType<NamespaceDeclarationSyntax>().ToList();
        if (blockNamespaces.Count is 0)
            return document;

        // Build the preamble (usings, etc. before the first namespace)
        var sourceText = (await document.GetTextAsync(ct).ConfigureAwait(false)).ToString();
        var firstNsStart = blockNamespaces[0].SpanStart;
        var preamble = sourceText.Substring(0, firstNsStart);

        // Collect the inner content of all namespace blocks, dedented
        var innerTexts = new List<string>();
        foreach (var ns in blockNamespaces)
        {
            CollectInnerText(ns, 1, innerTexts);
        }

        // Assemble the new file
        var sb = new System.Text.StringBuilder();
        sb.Append(preamble);
        sb.Append("namespace ");
        sb.Append(flattenedNamespace);
        sb.Append(';');
        sb.Append('\n');

        foreach (var innerText in innerTexts)
        {
            sb.Append(innerText);
        }

        var newSourceText = sb.ToString().TrimEnd();
        var newTree = CSharpSyntaxTree.ParseText(newSourceText, cancellationToken: ct);
        var newRoot = await newTree.GetRootAsync(ct).ConfigureAwait(false);

        return document.WithSyntaxRoot(newRoot);
    }

    private static void CollectInnerText(NamespaceDeclarationSyntax ns, int depth, List<string> innerTexts)
    {
        // If this namespace only contains a single child namespace (pure wrapper), recurse
        var childNamespaces = ns.Members.OfType<NamespaceDeclarationSyntax>().ToList();
        if (childNamespaces.Count is 1 && ns.Members.Count is 1)
        {
            CollectInnerText(childNamespaces[0], depth + 1, innerTexts);
            return;
        }

        // Extract text between { and }, dedented
        var openBrace = ns.OpenBraceToken;
        var closeBrace = ns.CloseBraceToken;
        var innerStart = openBrace.Span.End;
        var innerEnd = closeBrace.SpanStart;
        var fullText = ns.SyntaxTree.GetText().ToString();
        var innerContent = fullText.Substring(innerStart, innerEnd - innerStart);

        var dedented = DedentText(innerContent, depth);
        innerTexts.Add(dedented);
    }

    private static string DedentText(string text, int levels)
    {
        var lines = text.Split('\n');
        var sb = new System.Text.StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var dedented = DedentLine(line, levels);
            sb.Append(dedented);
            if (i < lines.Length - 1)
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string DedentLine(string line, int levels)
    {
        var remaining = line;
        for (var i = 0; i < levels; i++)
        {
            if (remaining.StartsWith("    ", StringComparison.Ordinal))
            {
                remaining = remaining.Substring(4);
            }
            else if (remaining.StartsWith("\t", StringComparison.Ordinal))
            {
                remaining = remaining.Substring(1);
            }
        }

        return remaining;
    }
}
