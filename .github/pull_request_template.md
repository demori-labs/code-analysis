# <!-- Insert Pull Request Title Here -->

## Overview

<!-- Provide a brief summary of the changes introduced and the problem they solve. -->

Resolves # (Insert issue number)

## Category of Change

- [ ] New analyser or code fix
- [ ] Bug resolution (non-breaking)
- [ ] Feature enhancement (non-breaking)
- [ ] Breaking change (modifies existing behaviour)
- [ ] Documentation or infrastructure update

## Core Requirements

- [ ] **Code Quality:** I have self-reviewed my work and manually verified its correctness. (Unverified or low-quality AI-generated submissions will be closed).
- [ ] **Formatting:** Code adheres to the existing style and has been formatted using CSharpier. Complex logic is appropriately commented.
- [ ] **Validation:** `dotnet build` completes with zero warnings, and `dotnet test` passes without any failures or skipped tests.
- [ ] **Housekeeping:** No credentials or sensitive data are included. `CHANGELOG.md` and relevant documentation have been updated.

## Analyser & Code Fix Details (If applicable)

- [ ] **Naming & Registration:** The rule ID follows the `DLxxxx` format and is properly registered in `RuleIdentifiers.cs` and `AnalyzerReleases.Unshipped.md`.
- [ ] **Uniqueness:** The analyser does not duplicate existing Microsoft CA rules.
- [ ] **Testing Coverage:** Unit tests comprehensively cover positive, negative, and edge-case scenarios.
- [ ] **Code Fix Accuracy:** The fix generates valid, compilable code whilst preserving syntax trivia (such as comments and whitespace).
- [ ] **Documentation:** A dedicated `docs/analysers/DLxxxx.md` file has been created, benchmark results are included, and the `README.md` table is updated.

## Performance Checklist (If applicable)

- [ ] **Syntax First:** Expensive semantic model queries are safeguarded by preliminary, lightweight syntax checks.
- [ ] **Caching:** Symbol lookups are cached within `RegisterCompilationStartAction` rather than being evaluated per-node.
- [ ] **Memory Efficiency:** Hot execution paths are completely allocation-free (strictly avoiding LINQ, closures, and string interpolation).
