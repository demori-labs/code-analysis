## Summary

<!-- What does this PR do and why? Link related issues with "Closes #123". -->

## Changes

<!-- Bulleted list of changes. For new analyzers, include rule ID and one-line description. -->

## Type of change

- [ ] New analyzer / code fix
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] Enhancement (non-breaking change that improves existing functionality)
- [ ] Breaking change (fix or feature that would cause existing behaviour to change)
- [ ] Documentation / infrastructure

## Checklist

### Required for all PRs

- [ ] `dotnet build` succeeds with **zero warnings** (`TreatWarningsAsErrors` is enabled)
- [ ] `dotnet test` passes **all tests** (no skipped or failing)
- [ ] Code is formatted with CSharpier (`dotnet csharpier .`)
- [ ] No secrets, credentials, or personal paths in committed files
- [ ] `CHANGELOG.md` updated under the appropriate version heading

### New or modified analyzer

- [ ] Rule ID follows the `DLxxxx` convention (1xxx Design, 2xxx Usage, 3xxx Style, 4xxx Complexity, 5xxx Performance)
- [ ] Rule registered in `RuleIdentifiers.cs` and `AnalyzerReleases.Unshipped.md`
- [ ] Tests cover positive, negative, and edge cases
- [ ] No overlap with existing rules or Microsoft CA rules (document related rules if applicable)
- [ ] Code fix preserves trivia (comments, whitespace) and produces valid compilable code
- [ ] Benchmarks created and results included in documentation
- [ ] Documentation added: `docs/analyzers/DLxxxx.md`, row in `README.md` rules table

### Performance

- [ ] Expensive semantic model calls guarded by cheap syntax pre-checks
- [ ] Symbol lookups cached in `RegisterCompilationStartAction`, not per-node
- [ ] No allocations in hot paths (no LINQ, string interpolation, or closures in callbacks)
