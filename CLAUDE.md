# DemoriLabs.CodeAnalysis Development Guide

## Project Overview

Roslyn analyzers and code fixes for C#, published as a NuGet package. Targets `netstandard2.0` for broad compatibility. Uses .NET 10 SDK.

## Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test (TUnit test runner)
dotnet run --project test/DemoriLabs.CodeAnalysis.Tests.Unit -- --treenode-filter "/*/*/ClassName/MethodName"

# Run benchmarks
dotnet run -c Release --project bench/DemoriLabs.CodeAnalysis.Benchmarks
```

## Publishing

Versioning is managed by Nerdbank.GitVersioning (CLI tool, not a package reference). Version is declared in `src/DemoriLabs.CodeAnalysis/version.json`.

- **Feature branches**: prerelease — `1.0.0-beta.{height}` (SemVer 2.0)
- **Main branch**: release — `1.0.0`

```bash
# Get the version (use NuGetPackageVersion for prerelease, SimpleVersion for release)
dotnet nbgv get-version -p src/DemoriLabs.CodeAnalysis -v NuGetPackageVersion
dotnet nbgv get-version -p src/DemoriLabs.CodeAnalysis -v SimpleVersion

# Build and pack
VERSION=$(dotnet nbgv get-version -p src/DemoriLabs.CodeAnalysis -v NuGetPackageVersion)
dotnet build DemoriLabs.CodeAnalysis.slnx -c Release
dotnet pack src/DemoriLabs.CodeAnalysis/DemoriLabs.CodeAnalysis.csproj -c Release -p:PackageVersion=$VERSION --no-build
```

## Architecture

Three source projects plus tests and benchmarks:

- **DemoriLabs.CodeAnalysis** — Analyzers (`DiagnosticAnalyzer` subclasses), organised into subdirectories by rule category (CognitiveComplexity, InvertIf, MultipleEnumeration, NamedArgument, ReadOnlyParameter, RecordDesign)
- **DemoriLabs.CodeAnalysis.CodeFixes** — Code fix providers (`CodeFixProvider` subclasses), one per fixable rule
- **DemoriLabs.CodeAnalysis.Attributes** — Zero-cost attributes (`[Mutable]`, `[ReadOnly]`, `[NamedArgument]`, etc.) used to annotate user code for analyzer behaviour

Shared constants live in `RuleIdentifiers.cs` (diagnostic IDs) and `RuleCategories.cs` (category strings).

## Diagnostic ID Convention

`DL` prefix, four digits. First digit = category:

- **DL1xxx** Design, **DL2xxx** Usage, **DL3xxx** Style, **DL4xxx** Complexity, **DL5xxx** Performance

## Build Configuration

- Central package version management (Directory.Packages.props in src/, test/, bench/)
- `TreatWarningsAsErrors` is enabled globally
- External analyzers: SonarAnalyzer.CSharp, Microsoft.VisualStudio.Threading.Analyzers, ReferenceTrimmer
- CSharpier (1.2.6) for formatting
