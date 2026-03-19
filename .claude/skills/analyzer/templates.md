# Code Templates

All templates for creating analyzers, code fixes, tests, and benchmarks.

## Analyzer template

File: `src/DemoriLabs.CodeAnalysis/<DirectoryName>/<AnalyzerName>.cs`

```csharp
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.<DirectoryName>;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class <AnalyzerName> : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.<ConstantName>,
        title: "<Human-readable title>",
        messageFormat: "<Message with optional {0} parameters>",
        RuleCategories.<Category>,
        DiagnosticSeverity.<Severity>,
        isEnabledByDefault: true,
        description: "<One-sentence description>."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Register appropriate action(s)
    }
}
```

**Performance rules:**

- Always call `context.EnableConcurrentExecution()`
- Always call `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`
- The class MUST be `sealed`
- **Syntax before semantics**: Guard expensive `SemanticModel`/`IOperation` calls with cheap syntax checks first (e.g. check node kind, modifiers, or parent type before calling `GetSymbolInfo`)
- **Cache symbol lookups**: Call `Compilation.GetTypeByMetadataName()` once inside `RegisterCompilationStartAction`, not per-node. If the symbol is `null`, bail out early (the target API isn't referenced)
- **Prefer `RegisterOperationAction`** over `RegisterSyntaxNodeAction` when analysing executable code semantically — the IOperation API is higher-level, more performant, and language-agnostic
- **Use `RegisterSymbolStartAction`** when analysis is scoped to a single symbol and its members (e.g. checking all members of a type) — this avoids processing unrelated symbols
- **Avoid allocations in hot paths**: No string interpolation, no LINQ in tight loops, no closures capturing mutable state. Use `Span<T>`, `stackalloc`, or `string.Equals` with `StringComparison` overloads
- **Use static lambdas** (`static (ctx) => ...`) for registration callbacks to prevent accidental closure captures. If state is needed, pass it via a tuple or dedicated context object
- **Compare symbols correctly**: Always use `SymbolEqualityComparer.Default` (or `.IncludeNullability`) — never `==` or bare `.Equals()` on `ISymbol`
- For generic types, use the backtick-arity metadata name: `"System.Collections.Generic.Dictionary`2"`, not the C# generic syntax

### Analyzer with symbol caching (CompilationStartAction)

When the analyzer needs to resolve external types (attributes, interfaces, base classes), cache them at compilation start:

```csharp
public override void Initialize(AnalysisContext context)
{
    context.EnableConcurrentExecution();
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

    context.RegisterCompilationStartAction(static compilationContext =>
    {
        var targetType = compilationContext.Compilation.GetTypeByMetadataName("System.IDisposable");
        if (targetType is null)
            return; // Target API not referenced — nothing to analyse

        compilationContext.RegisterOperationAction(
            operationContext => AnalyzeOperation(operationContext, targetType),
            OperationKind.Invocation
        );
    });
}

private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol targetType)
{
    // targetType is already resolved — no per-node GetTypeByMetadataName calls
}
```

### Choosing a registration action

| Action                           | Use when                                                                                                                                       |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `RegisterSyntaxNodeAction`       | Pure syntax checks (no semantic model needed), or cheap syntactic pre-filtering before a semantic check                                        |
| `RegisterOperationAction`        | Analysing executable code semantically (invocations, assignments, returns, etc.) — preferred over `RegisterSyntaxNodeAction` for semantic work |
| `RegisterSymbolAction`           | Type-level or member-level analysis using symbol properties (modifiers, base types, interfaces)                                                |
| `RegisterSymbolStartAction`      | Scoped analysis of a symbol and its members — register nested actions that only fire for that symbol                                           |
| `RegisterCompilationStartAction` | Caching compilation-wide state (type symbols, options) before registering nested actions                                                       |

## Code fix template

File: `src/DemoriLabs.CodeAnalysis.CodeFixes/<DirectoryName>/<CodeFixName>.cs`

```csharp
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DemoriLabs.CodeAnalysis.CodeFixes.<DirectoryName>;

[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class <CodeFixName> : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [RuleIdentifiers.<ConstantName>];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                "<Action title>",
                ct => FixAsync(context.Document, node, ct),
                equivalenceKey: nameof(<CodeFixName>)
            ),
            diagnostic
        );
    }
}
```

**Rules:**

- Always use `ConfigureAwait(false)` on all awaits
- Always provide an `equivalenceKey` — use `nameof(CodeFixClassName)` for a single-fix provider. If the provider registers multiple distinct fixes, use separate equivalence keys per fix so FixAll applies only the intended fix
- Always return `WellKnownFixAllProviders.BatchFixer` from `GetFixAllProvider()` — this is safe when: (a) fix spans don't overlap between diagnostics, (b) code actions only contain `ApplyChangesOperation`, and (c) equivalence keys are non-null. If any of these don't hold (e.g. non-local fixes that add/remove declarations, or overlapping spans), implement a custom `FixAllProvider` instead
- The class MUST be `sealed`
- Avoid allocations in `RegisterCodeFixesAsync` — it runs on every keystroke in the IDE when the cursor is on a diagnostic. Keep it minimal: find the node, register the action, defer real work to the async fix method
- **Generated code must follow project style** — the code produced by the fix must match hand-written code conventions:
    - Prefer pattern matching (`is`, `is not`, `switch` expressions) over type casts and conditional chains
    - Use `is null` / `is not null` instead of `== null` / `!= null`
    - Use `is false` instead of `!` for boolean negation
    - Use modern C# syntax: file-scoped namespaces, target-typed `new()`, collection expressions, etc.

## Analyzer test template

File: `test/DemoriLabs.CodeAnalysis.Tests.Unit/<DirectoryName>/<AnalyzerName>Tests.cs`

Use partial classes and additional files like `<AnalyzerName>Tests.<Category>.cs` if there are many test cases.

```csharp
using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.<DirectoryName>;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.<DirectoryName>;

// ReSharper disable MemberCanBeMadeStatic.Global
public class <AnalyzerName>Tests
{
    private static CSharpAnalyzerTest<<AnalyzerName>, DefaultVerifier> CreateTest(
        [StringSyntax("C#")] string source
    )
    {
        return new CSharpAnalyzerTest<<AnalyzerName>, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task <DescriptiveScenario>_ReportsDiagnostic()
    {
        var test = CreateTest(
            """
            // C# source with {|DLxxxx:markedSpan|} for expected diagnostics
            """
        );

        await test.RunAsync();
    }

    [Test]
    public async Task <DescriptiveScenario>_NoDiagnostic()
    {
        var test = CreateTest(
            """
            // C# source with NO diagnostic markers — must compile cleanly
            """
        );

        await test.RunAsync();
    }
}
```

### Adding custom attribute references

If tests need access to custom attributes from `DemoriLabs.CodeAnalysis.Attributes`:

```csharp
var test = new CSharpAnalyzerTest<...>
{
    TestCode = source,
    ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
};
test.TestState.AdditionalReferences.Add(typeof(SomeAttribute).Assembly);
return test;
```

## Code fix test template

File: `test/DemoriLabs.CodeAnalysis.Tests.Unit/<DirectoryName>/<CodeFixName>Tests.cs`

```csharp
using System.Diagnostics.CodeAnalysis;
using DemoriLabs.CodeAnalysis.CodeFixes;
using DemoriLabs.CodeAnalysis.<DirectoryName>;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace DemoriLabs.CodeAnalysis.Tests.Unit.<DirectoryName>;

// ReSharper disable MemberCanBeMadeStatic.Global
public class <CodeFixName>Tests
{
    private static CSharpCodeFixTest<
        <AnalyzerName>,
        <CodeFixName>,
        DefaultVerifier
    > CreateTest([StringSyntax("C#")] string source, [StringSyntax("C#")] string fixedSource)
    {
        return new CSharpCodeFixTest<<AnalyzerName>, <CodeFixName>, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
    }

    [Test]
    public async Task <Scenario>_AppliesFix()
    {
        var test = CreateTest(
            """
            // source with {|DLxxxx:markers|}
            """,
            """
            // expected fixed source
            """
        );

        await test.RunAsync();
    }
}
```

## Analyzer benchmark template

File: `bench/DemoriLabs.CodeAnalysis.Benchmarks/<DirectoryName>/<AnalyzerName>Benchmarks.cs`

```csharp
using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.<DirectoryName>;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.<DirectoryName>;

[MemoryDiagnoser]
public class <AnalyzerName>Benchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            // Representative C# source that exercises the analyzer
            // Include both diagnostic-triggering and clean code
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new <AnalyzerName>());
    }
}
```

## Code fix benchmark template

File: `bench/DemoriLabs.CodeAnalysis.Benchmarks/<DirectoryName>/<CodeFixName>Benchmarks.cs`

```csharp
using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.CodeFixes;
using DemoriLabs.CodeAnalysis.<DirectoryName>;
using Microsoft.CodeAnalysis;

namespace DemoriLabs.CodeAnalysis.Benchmarks.<DirectoryName>;

[MemoryDiagnoser]
public class <CodeFixName>Benchmarks
{
    private CodeFixRunner _runner = null!;
    private readonly <CodeFixName> _codeFix = new();

    [GlobalSetup]
    public async Task Setup()
    {
        _runner = await CodeFixRunner.CreateAsync<<AnalyzerName>>(
            """
            // Source that triggers the diagnostic
            """
        );
    }

    [Benchmark]
    public async Task<Solution> ApplyFix()
    {
        return await _runner.ApplyFixAsync(_codeFix);
    }
}
```
