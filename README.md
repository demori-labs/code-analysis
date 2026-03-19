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
| [DL2002](docs/analyzers/DL2001-DL2002.md) | `[ReadOnly]` is incompatible with parameter modifier |
| [DL2003](docs/analyzers/DL2003.md)        | Primary constructor parameter should be `[ReadOnly]` |
| [DL3001](docs/analyzers/DL3001.md)        | Use named arguments                                  |
| [DL3002](docs/analyzers/DL3002.md)        | Invert `if` statement to reduce nesting              |
| [DL4001](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is moderate                     |
| [DL4002](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is elevated                     |
| [DL5001](docs/analyzers/DL5001.md)        | Possible multiple enumeration                        |

See each rule's documentation for severity, code fix availability, configuration options, and attributes.
