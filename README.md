# DemoriLabs.CodeAnalysis

[![License: MIT](https://img.shields.io/github/license/demori-labs/diagnostics)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DemoriLabs.CodeAnalysis)](https://www.nuget.org/packages/DemoriLabs.CodeAnalysis)

Roslyn analyzers and code fixes for C#, designed to enforce design patterns, catch common mistakes, and improve code quality.

## Rules

| Rule                                      | Description                                          |
| ----------------------------------------- | ---------------------------------------------------- |
| [DL1001](docs/analyzers/DL1001.md)        | Records should not have mutable properties           |
| [DL1002](docs/analyzers/DL1002.md)        | Records should not have mutable property types       |
| [DL1003](docs/analyzers/DL1003.md)        | Record has too many positional parameters            |
| [DL1004](docs/analyzers/DL1004.md)        | Data class could be a record                         |
| [DL1005](docs/analyzers/DL1005.md)        | Type can use a primary constructor                   |
| [DL2001](docs/analyzers/DL2001-DL2002.md) | Parameter must not be reassigned                     |
| [DL2002](docs/analyzers/DL2001-DL2002.md) | Incompatible attribute on parameter                  |
| [DL2003](docs/analyzers/DL2003.md)        | Primary constructor parameter should be `[ReadOnly]` |
| [DL2004](docs/analyzers/DL2004.md)        | Method parameter should be `[ReadOnly]`              |
| [DL2005](docs/analyzers/DL2005.md)        | Parameter is never used in method body               |
| [DL3001](docs/analyzers/DL3001.md)        | Use named arguments                                  |
| [DL3002](docs/analyzers/DL3002.md)        | Invert `if` statement to reduce nesting              |
| [DL3003](docs/analyzers/DL3003.md)        | Use constant pattern instead of equality operator    |
| [DL3004](docs/analyzers/DL3004.md)        | Use `is false` / `is not` instead of `!`             |
| [DL3005](docs/analyzers/DL3005.md)        | Use logical pattern for combined comparisons         |
| [DL3006](docs/analyzers/DL3006.md)        | Use declaration pattern instead of type check + cast |
| [DL3007](docs/analyzers/DL3007.md)        | Use declaration pattern instead of `as` + null check |
| [DL3008](docs/analyzers/DL3008.md)        | Simplify boolean return                              |
| [DL3009](docs/analyzers/DL3009.md)        | Simplify boolean assignment                          |
| [DL3010](docs/analyzers/DL3010.md)        | Simplify conditional return to ternary               |
| [DL3011](docs/analyzers/DL3011.md)        | Simplify conditional assignment to ternary           |
| [DL3012](docs/analyzers/DL3012.md)        | Merge nested `if` statements                         |
| [DL3013](docs/analyzers/DL3013.md)        | Use null-coalescing operator (`??`)                  |
| [DL3014](docs/analyzers/DL3014.md)        | Use null-coalescing assignment (`??=`)               |
| [DL3015](docs/analyzers/DL3015.md)        | Use null-conditional assignment (`?.`)               |
| [DL3016](docs/analyzers/DL3016.md)        | Redundant type pattern                               |
| [DL3017](docs/analyzers/DL3017.md)        | Use `string.Equals` with `StringComparison`          |
| [DL4001](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is moderate                     |
| [DL4002](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is elevated                     |
| [DL5001](docs/analyzers/DL5001.md)        | Possible multiple enumeration                        |

See each rule's documentation for severity, code fix availability, configuration options, and attributes.
