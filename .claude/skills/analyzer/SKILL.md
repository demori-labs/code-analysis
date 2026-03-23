---
name: analyzer
description: Create, modify, or review Roslyn analyzers, code fixes, tests, benchmarks, and documentation. Use when the user asks to create, modify, or review a diagnostic, analyzer, rule, or code fix for this project.
argument-hint: what to do (e.g. "create an analyzer for X" or "review DL1003")
---

# Analyzer Workflow

Applies when creating or reviewing Roslyn analyzers and code fixes in DemoriLabs.CodeAnalysis.

The user's request: $ARGUMENTS

## Creating a new analyzer

Follow these steps in order. Do not skip steps.

### Step 0 — Plan

Enter plan mode using `EnterPlanMode` before writing any code. If the request is ambiguous or missing details, use `AskUserQuestion` to clarify before finalising the plan.

Your plan must cover:

1. **Rule ID**: Next available `DLxxxx` from `src/DemoriLabs.CodeAnalysis/RuleIdentifiers.cs`. First digit = category: 1=Design, 2=Usage, 3=Style, 4=Complexity, 5=Performance.
2. **Category & severity**: From `RuleCategories` (Design, Usage, Style, Complexity, Performance, Security, Naming). Severity: Hidden, Info, Warning, or Error.
3. **Registration action**: Choose based on what you're analysing — see the action selection table in [templates.md](templates.md). Prefer `RegisterOperationAction` for semantic analysis of executable code; use `RegisterSyntaxNodeAction` only for pure syntax checks or syntactic pre-filtering.
4. **Code fix**: Whether feasible and what transformation it performs.
5. **Directory name**: Short PascalCase name (e.g. `InvertIf`, `MultipleEnumeration`).

Once the plan is complete, use `ExitPlanMode` to request user approval before proceeding.

### Step 1 — Register the rule

Add constant(s) to `src/DemoriLabs.CodeAnalysis/RuleIdentifiers.cs` in the correct category section.

### Step 2 — Write tests FIRST (TDD)

Create comprehensive tests **before** any implementation. See [templates.md](templates.md) for exact patterns.

Tests go under `test/DemoriLabs.CodeAnalysis.Tests.Unit/<DirectoryName>/`.

**Coverage requirements** — you MUST cover:

- **Happy path**: Core scenario(s) where the diagnostic fires
- **Edge cases**: Boundary conditions, unusual syntax, nested scopes, partial classes
- **No-diagnostic cases**: Similar code that should NOT trigger
- **Multiple occurrences**: Multiple diagnostics in a single file
- **Different contexts**: Methods, constructors, properties, lambdas, local functions — whichever are relevant
- **Attribute suppression**: If the rule supports a suppression attribute
- **Code fix edge cases** (if applicable): Fix produces valid, compilable code in all scenarios

Aim for 8-15 analyzer tests and 3-5 code fix tests. Quality over quantity, but no obvious gaps.

### Step 3 — Implement the analyzer

Create `src/DemoriLabs.CodeAnalysis/<DirectoryName>/<AnalyzerName>.cs`. See [templates.md](templates.md) for the exact pattern.

### Step 4 — Implement the code fix (if applicable)

Create `src/DemoriLabs.CodeAnalysis.CodeFixes/<DirectoryName>/<CodeFixName>.cs`. See [templates.md](templates.md) for the exact pattern.

### Step 5 — Verify all tests pass

Run `dotnet test`. Fix the implementation (not the tests) until all pass. Zero warnings required (`TreatWarningsAsErrors` is enabled).

### Step 6 — Benchmarks

Create benchmarks under `bench/DemoriLabs.CodeAnalysis.Benchmarks/`. See [templates.md](templates.md) for patterns. Then **run** them to capture the baseline:

```bash
dotnet run -c Release --project bench/DemoriLabs.CodeAnalysis.Benchmarks -- --filter "*<AnalyzerName>*"
```

The results (Mean, Error, StdDev, Allocated) are required for the documentation in the next step.

### Step 7 — Documentation

See [documentation.md](documentation.md) for all templates:

- Add row to `src/DemoriLabs.CodeAnalysis/AnalyzerReleases.Unshipped.md`
- Create `docs/analyzers/DLxxxx.md` — **must include the Benchmarks section** with results from Step 6
- Add row to `README.md` rules table

### Step 8 — Final verification

Run `dotnet build && dotnet test`. Everything must compile with zero warnings and all tests must pass.

## Reviewing an existing analyzer

When reviewing or modifying an existing analyzer, check against [review-checklist.md](review-checklist.md).

## Rules (always apply)

- Use the `csharp-code-principles` skill patterns (pattern matching, `is false`, modern C# syntax)
- Never guess TUnit CLI commands — use `dotnet test` for the full suite or `dotnet run --project test/DemoriLabs.CodeAnalysis.Tests.Unit -- --treenode-filter "/*/*/ClassName/MethodName"` for individual tests
- **Performance is critical** — analysers run on every keystroke in the IDE:
    - Guard expensive semantic model / IOperation calls with cheap syntax pre-checks
    - Cache `GetTypeByMetadataName` lookups in `RegisterCompilationStartAction`, not per-node
    - Use `static` lambdas for registration callbacks to avoid closure allocations
    - No string interpolation, LINQ, or closure captures in hot paths
    - Compare symbols with `SymbolEqualityComparer.Default`, never `==`
    - Prefer `RegisterOperationAction` over `RegisterSyntaxNodeAction` for semantic executable code analysis
- All analyzer and code fix classes must be `sealed`
- **Code fix output must follow project style** — code produced by code fixes must match the same conventions as hand-written code:
    - Prefer pattern matching (`is`, `is not`, `switch` expressions) over type casts and conditional chains
    - Use `is null` / `is not null` instead of `== null` / `!= null`
    - Use `is false` instead of `!` for boolean negation
    - Use modern C# syntax: file-scoped namespaces, target-typed `new()`, collection expressions, etc.
- Format with CSharpier conventions (the formatter will run on save)
