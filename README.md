# DemoriLabs.CodeAnalysis

[![License: MIT](https://img.shields.io/github/license/demori-labs/diagnostics)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DemoriLabs.CodeAnalysis)](https://www.nuget.org/packages/DemoriLabs.CodeAnalysis)

Roslyn analyzers and code fixes for C#, designed to enforce design patterns, catch common mistakes, and improve code quality.

## Rules

### Design (DL1xxx)

| Rule | Description | Severity | Code Fix |
| ---- | ----------- | -------- | -------- |
| [DL1001](docs/analyzers/DL1001.md) | Records should not have mutable properties | Warning | No |
| [DL1002](docs/analyzers/DL1002.md) | Records should not have mutable property types | Warning | No |
| [DL1003](docs/analyzers/DL1003.md) | Record has too many positional parameters | Warning | No |
| [DL1004](docs/analyzers/DL1004.md) | Data class could be a record | Info | Yes |
| [DL1005](docs/analyzers/DL1005.md) | Type can use a primary constructor | Warning | Yes |

### Usage (DL2xxx)

| Rule | Description | Severity | Code Fix |
| ---- | ----------- | -------- | -------- |
| [DL2001](docs/analyzers/DL2001-DL2002.md) | Parameter must not be reassigned | Error | No |
| [DL2002](docs/analyzers/DL2001-DL2002.md) | Incompatible attribute on parameter | Error | No |
| [DL2003](docs/analyzers/DL2003.md) | Primary constructor parameter should be `[ReadOnly]` | Warning | Yes |
| [DL2004](docs/analyzers/DL2004.md) | Method parameter should be `[ReadOnly]` | Info | Yes |
| [DL2005](docs/analyzers/DL2005.md) | Parameter is never used in method body | Warning | No |

### Style (DL3xxx)

| Rule | Description | Severity | Code Fix |
| ---- | ----------- | -------- | -------- |
| [DL3001](docs/analyzers/DL3001.md) | Use named arguments | Warning | Yes |
| [DL3002](docs/analyzers/DL3002.md) | Invert `if` statement to reduce nesting | Warning | Yes |
| [DL3003](docs/analyzers/DL3003.md) | Use constant pattern instead of equality operator | Warning | Yes |
| [DL3004](docs/analyzers/DL3004.md) | Use `is false` / `is not` instead of `!` | Warning | Yes |
| [DL3005](docs/analyzers/DL3005.md) | Use logical pattern for combined comparisons | Warning | Yes |
| [DL3006](docs/analyzers/DL3006.md) | Use declaration pattern instead of type check + cast | Warning | Yes |
| [DL3007](docs/analyzers/DL3007.md) | Use declaration pattern instead of `as` + null check | Warning | Yes |
| [DL3008](docs/analyzers/DL3008.md) | Simplify boolean return | Warning | Yes |
| [DL3009](docs/analyzers/DL3009.md) | Simplify boolean assignment | Warning | Yes |
| [DL3010](docs/analyzers/DL3010.md) | Simplify conditional return to ternary | Info | Yes |
| [DL3011](docs/analyzers/DL3011.md) | Simplify conditional assignment to ternary | Warning | Yes |
| [DL3012](docs/analyzers/DL3012.md) | Merge nested `if` statements | Warning | Yes |
| [DL3013](docs/analyzers/DL3013.md) | Use null-coalescing operator (`??`) | Warning | Yes |
| [DL3014](docs/analyzers/DL3014.md) | Use null-coalescing assignment (`??=`) | Warning | Yes |
| [DL3015](docs/analyzers/DL3015.md) | Use null-conditional assignment (`?.`) | Warning | Yes |
| [DL3016](docs/analyzers/DL3016.md) | Redundant type pattern | Warning | Yes |
| [DL3017](docs/analyzers/DL3017.md) | Use `string.Equals` with `StringComparison` | Warning | Yes |

### Complexity (DL4xxx)

| Rule | Description | Severity | Code Fix |
| ---- | ----------- | -------- | -------- |
| [DL4001](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is moderate | Info | No |
| [DL4002](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is elevated | Warning | No |

### Performance (DL5xxx)

| Rule | Description | Severity | Code Fix |
| ---- | ----------- | -------- | -------- |
| [DL5001](docs/analyzers/DL5001.md) | Possible multiple enumeration | Warning | Yes |
| [DL5002](docs/analyzers/DL5002.md) | Use `string.IsNullOrEmpty` | Warning | Yes |

See each rule's documentation for configuration options and attributes.
