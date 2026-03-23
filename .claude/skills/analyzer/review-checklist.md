# Analyzer Review Checklist

Use this checklist when reviewing or modifying an existing analyzer or code fix.

## Analyzer implementation

- [ ] Class is `sealed`
- [ ] `EnableConcurrentExecution()` is called
- [ ] `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` is called
- [ ] `DiagnosticDescriptor` fields are `private static readonly`
- [ ] Rule ID matches `RuleIdentifiers` constant
- [ ] Category matches `RuleCategories` constant
- [ ] Message format is clear and actionable to the user
- [ ] Syntax pre-checks guard semantic model / IOperation access (cheap before expensive)
- [ ] Hot paths avoid allocations (no string interpolation, LINQ in tight loops, no closure captures — use `static` lambdas)
- [ ] Type symbol lookups via `GetTypeByMetadataName` are cached in `RegisterCompilationStartAction`, not called per-node
- [ ] Symbol comparisons use `SymbolEqualityComparer.Default`, not `==` or bare `.Equals()`
- [ ] `RegisterOperationAction` used instead of `RegisterSyntaxNodeAction` when semantic analysis of executable code is needed
- [ ] Generated code is excluded (via the `ConfigureGeneratedCodeAnalysis` call)
- [ ] Registration action choice is appropriate (see templates.md action selection table)

## Code fix implementation

- [ ] Class is `sealed`
- [ ] `[ExportCodeFixProvider(LanguageNames.CSharp)]` and `[Shared]` attributes present
- [ ] `GetFixAllProvider()` returns `WellKnownFixAllProviders.BatchFixer` (or a custom provider if fix spans overlap or fixes are non-local)
- [ ] `equivalenceKey` is provided and non-null in `CodeAction.Create`
- [ ] All `await` calls use `ConfigureAwait(false)`
- [ ] `RegisterCodeFixesAsync` is lightweight — no heavy computation, just node lookup and action registration
- [ ] Fix produces valid, compilable code in all cases
- [ ] `using` directives are added when needed (via `NamespaceImportResolver.EnsureUsingDirective`)
- [ ] Fix handles edge cases: missing nodes, partial classes, nested types
- [ ] Generated code follows project style: pattern matching, `is null`/`is not null`, `is false` over `!`, modern C# syntax (file-scoped namespaces, target-typed `new()`, collection expressions)

## Tests

- [ ] Analyzer tests exist with `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>`
- [ ] Code fix tests exist with `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>`
- [ ] `ReferenceAssemblies = ReferenceAssemblies.Net.Net100` is set
- [ ] Diagnostic markers use correct format: `{|DLxxxx:span|}`
- [ ] Happy path: core scenarios where the diagnostic fires
- [ ] Edge cases: unusual syntax, nested scopes, partial classes
- [ ] No-diagnostic cases: similar code that should NOT trigger
- [ ] Multiple occurrences in a single file
- [ ] Relevant contexts covered: methods, constructors, lambdas, local functions
- [ ] Attribute suppression tested (if applicable)
- [ ] Code fix tests verify the fix produces exact expected output
- [ ] All test methods have descriptive names: `<Scenario>_ReportsDiagnostic`, `<Scenario>_NoDiagnostic`, `<Scenario>_AppliesFix`

## Benchmarks

- [ ] Analyzer benchmark exists under `bench/.../<Category>/`
- [ ] Code fix benchmark exists under `bench/.../<Category>/` (if code fix exists)
- [ ] `[MemoryDiagnoser]` attribute is present
- [ ] Benchmark source is representative (includes both triggering and clean code)
- [ ] Benchmarks compile: `dotnet build -c Release bench/DemoriLabs.CodeAnalysis.Benchmarks`

## Documentation

- [ ] Row exists in `AnalyzerReleases.Unshipped.md` (or `.Shipped.md` if released)
- [ ] Markdown doc exists in `docs/analyzers/DLxxxx.md`
- [ ] Doc has: metadata table, Why section, Examples (violation + fixed + no-diagnostic), Configuration
- [ ] Code fix section present if code fix exists
- [ ] Row exists in `README.md` rules table, sorted by rule ID
- [ ] Links are correct (shared pages like `DL2001-DL2002.md` if applicable)

## Performance (for modifications)

The benchmark table in `docs/analyzers/DLxxxx.md` is the performance baseline for each analyzer.

- [ ] Read the existing benchmark table from the doc — this is the baseline to compare against
- [ ] If no benchmark table exists, run benchmarks first and add results to the doc **before** making changes
- [ ] After changes, run benchmarks: `dotnet run -c Release --project bench/DemoriLabs.CodeAnalysis.Benchmarks -- --filter "*<AnalyzerName>*"`
- [ ] Compare Mean and Allocated against the baseline — flag any regression
- [ ] Update the benchmark table in `docs/analyzers/DLxxxx.md` with the new results
- [ ] No new allocations introduced in analysis hot paths (closures, string interpolation, LINQ)
- [ ] Semantic model access is minimised and guarded by syntax pre-checks
- [ ] Registration callbacks use `static` lambdas where possible to avoid closure allocations
- [ ] Symbol comparisons use `SymbolEqualityComparer`, not reference equality
