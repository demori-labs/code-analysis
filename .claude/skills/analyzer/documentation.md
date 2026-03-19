# Documentation Templates

Templates for all documentation that must accompany a new or modified analyzer.

## AnalyzerReleases.Unshipped.md

File: `src/DemoriLabs.CodeAnalysis/AnalyzerReleases.Unshipped.md`

Add a row under `### New Rules`:

```
DLxxxx | <Category> | <Severity> | <AnalyzerClassName>
```

Example:

```
DL5001 | Performance | Warning | MultipleEnumerationAnalyzer
```

## Diagnostic markdown documentation

File: `docs/analyzers/DLxxxx.md`

If two closely related rules share a page (e.g. DL2001-DL2002), name the file `DLxxxx-DLyyyy.md`.

All docs **must** follow this section order exactly. Omit optional sections if not applicable, but never reorder.

````markdown
# DLxxxx - <Title>

<One-sentence summary of what the rule detects.>

| Rule   | Category   | Default Severity | Code Fix |
| ------ | ---------- | ---------------- | -------- |
| DLxxxx | <Category> | <Severity>       | Yes/No   |

## Why

<2-4 sentences explaining why this pattern is problematic.>

## <Rule detail sections> (optional, pick the appropriate heading)

Use one or more of these headings when the rule needs deeper explanation beyond "Why":

- **Scope** — what types/members the rule applies to (e.g. DL1001, DL2003)
- **When the diagnostic fires** — precise conditions that trigger the rule (e.g. DL3001, DL3002)
- **When the diagnostic does NOT fire** — conditions that prevent the rule from firing (e.g. DL3001, DL3002)
- **Detection criteria** — how the analyzer decides what to flag (e.g. DL1004)
- **What counts as <X>** — defines what the analyzer considers as X (e.g. DL5001 "What counts as enumeration")
- **Detected <X> types** — lists specific types/patterns detected (e.g. DL1002)
- **Supported contexts** — table of contexts and exit strategies (e.g. DL3002)

## Examples

### Violation

```csharp
// Code that triggers the diagnostic, with inline // DLxxxx comments
```

### Fixed

```csharp
// Corrected code
```

### No diagnostic

```csharp
// Similar code that correctly does NOT trigger
```

## Suppression (optional)

Include when the rule supports suppression via attributes (e.g. `[Mutable]`, `[NamedArgument]`).
Document each suppression mechanism with a code example.

## Code fix (optional, omit entirely if no code fix)

<Description of what the code fix does — explain the transformation, not just "applies a fix".>

## Benchmarks

Measured on Apple M1 Pro, .NET 10.0.5. Source: [<AnalyzerName>Benchmarks.cs](../../bench/DemoriLabs.CodeAnalysis.Benchmarks/<DirectoryName>/<AnalyzerName>Benchmarks.cs), [<CodeFixName>Benchmarks.cs](../../bench/DemoriLabs.CodeAnalysis.Benchmarks/<DirectoryName>/<CodeFixName>Benchmarks.cs).

| Benchmark | Mean     | Error    | StdDev   | Allocated |
| --------- | -------- | -------- | -------- | --------- |
| Analyze   | x.xxx ms | x.xxx ms | x.xxx ms | xxx.xx KB |
| ApplyFix  | x.xxx us | x.xxx us | x.xxx us | xxx.xx KB |

Omit the ApplyFix row and the code fix source link if there is no code fix.

This table is the **performance baseline**. Future modifications must compare against these numbers and update the table with new results.

## Configuration

```ini
# Adjust severity
dotnet_diagnostic.DLxxxx.severity = error

# Disable entirely
dotnet_diagnostic.DLxxxx.severity = none
```
````

### Canonical section order (mandatory)

1. **Title** — `# DLxxxx - <Title>` + one-sentence summary + metadata table
2. **Why** — explains the problem
3. **Rule detail sections** — optional, rule-specific depth (Scope, When fires, Detection criteria, etc.)
4. **Examples** — Violation → Fixed → No diagnostic
5. **Suppression** — optional, only if rule supports attribute-based suppression
6. **Code fix** — optional, only if a code fix exists
7. **Benchmarks** — always present, always second-to-last
8. **Configuration** — always present, always last

### Documentation quality checklist

- Title matches the DiagnosticDescriptor title
- "Why" section explains the problem, not just the rule
- Violation examples show real-world patterns, not toy code
- No-diagnostic examples cover the most likely false-positive concerns
- Code fix section explains the transformation, not just "applies a fix"
- If the rule supports `.editorconfig` options or suppression attributes, document them

## README.md rules table

File: `README.md`

Add a row maintaining sort order by rule ID:

```markdown
| [DLxxxx](docs/analyzers/DLxxxx.md) | <Short description> |
```

If the rule shares a doc page with another rule, link to the shared page:

```markdown
| [DL2001](docs/analyzers/DL2001-DL2002.md) | Parameter must not be reassigned |
```
