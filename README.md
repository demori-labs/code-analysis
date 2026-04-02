# DemoriLabs.CodeAnalysis

[![License: MIT NON-AI](https://img.shields.io/badge/License-MIT%20NON--AI-blue)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DemoriLabs.CodeAnalysis)](https://www.nuget.org/packages/DemoriLabs.CodeAnalysis)

An opinionated suite of Roslyn analysers and code fixes tailored for modern C#. By adopting a strict, minimal-configuration philosophy, this package minimises trivial decision-making and enforces straightforward, pragmatic C# idioms out of the box. Every rule is comprehensively documented with clear reasoning regarding its design choices and underlying trade-offs. The behaviour is intentionally rigid to guarantee architectural consistency; if a specific analyser does not suit your context, the primary recourse is simply to disable it.

Furthermore, the style analysers deliberately favour a more functional approach within C#. They heavily bias towards pattern matching and explicit constructs, optimising for code that is straightforward to read and actively flagging legacy features that obscure intent.

Pairing this package with an AI coding assistant yields a highly effective workflow. The analysers act as a rigorous, automated baseline, ensuring that any generated code adheres to a uniform, pragmatic style—managing the finer details, from strict null checks to minimising cognitive complexity.

## Rules

### Design (DL1xxx)

| Rule                               | Description                                    | Severity | Code Fix |
| ---------------------------------- | ---------------------------------------------- | -------- | -------- |
| [DL1001](docs/analyzers/DL1001.md) | Records should not have mutable properties     | Warning  | No       |
| [DL1002](docs/analyzers/DL1002.md) | Records should not have mutable property types | Warning  | No       |
| [DL1003](docs/analyzers/DL1003.md) | Record has too many positional parameters      | Warning  | No       |
| [DL1004](docs/analyzers/DL1004.md) | Data class could be a record                   | Info     | Yes      |
| [DL1005](docs/analyzers/DL1005.md) | Type can use a primary constructor             | Warning  | Yes      |

### Usage (DL2xxx)

| Rule                                      | Description                                          | Severity | Code Fix |
| ----------------------------------------- | ---------------------------------------------------- | -------- | -------- |
| [DL2001](docs/analyzers/DL2001-DL2002.md) | Parameter must not be reassigned                     | Error    | No       |
| [DL2002](docs/analyzers/DL2001-DL2002.md) | Incompatible attribute on parameter                  | Error    | No       |
| [DL2003](docs/analyzers/DL2003.md)        | Primary constructor parameter should be `[ReadOnly]` | Warning  | Yes      |
| [DL2004](docs/analyzers/DL2004.md)        | Method parameter should be `[ReadOnly]`              | Info     | Yes      |
| [DL2005](docs/analyzers/DL2005.md)        | Parameter is never used in method body               | Warning  | No       |

### Style (DL3xxx)

| Rule                               | Description                                          | Severity | Code Fix |
| ---------------------------------- | ---------------------------------------------------- | -------- | -------- |
| [DL3001](docs/analyzers/DL3001.md) | Use named arguments                                  | Warning  | Yes      |
| [DL3002](docs/analyzers/DL3002.md) | Invert `if` statement to reduce nesting              | Warning  | Yes      |
| [DL3003](docs/analyzers/DL3003.md) | Use constant pattern instead of equality operator    | Warning  | Yes      |
| [DL3004](docs/analyzers/DL3004.md) | Use `is false` / `is not` instead of `!`             | Warning  | Yes      |
| [DL3005](docs/analyzers/DL3005.md) | Use logical pattern for combined comparisons         | Warning  | Yes      |
| [DL3006](docs/analyzers/DL3006.md) | Use declaration pattern instead of type check + cast | Warning  | Yes      |
| [DL3007](docs/analyzers/DL3007.md) | Use declaration pattern instead of `as` + null check | Warning  | Yes      |
| [DL3008](docs/analyzers/DL3008.md) | Simplify boolean return                              | Warning  | Yes      |
| [DL3009](docs/analyzers/DL3009.md) | Simplify boolean assignment                          | Warning  | Yes      |
| [DL3010](docs/analyzers/DL3010.md) | Simplify conditional return to ternary               | Info     | Yes      |
| [DL3011](docs/analyzers/DL3011.md) | Simplify conditional assignment to ternary           | Warning  | Yes      |
| [DL3012](docs/analyzers/DL3012.md) | Merge nested `if` statements                         | Warning  | Yes      |
| [DL3013](docs/analyzers/DL3013.md) | Use null-coalescing operator (`??`)                  | Warning  | Yes      |
| [DL3014](docs/analyzers/DL3014.md) | Use null-coalescing assignment (`??=`)               | Warning  | Yes      |
| [DL3015](docs/analyzers/DL3015.md) | Use null-conditional assignment (`?.`)               | Warning  | Yes      |
| [DL3016](docs/analyzers/DL3016.md) | Redundant type pattern                               | Warning  | Yes      |
| [DL3017](docs/analyzers/DL3017.md) | Use `string.Equals` with `StringComparison`          | Warning  | Yes      |

### Complexity (DL4xxx)

| Rule                                      | Description                      | Severity | Code Fix |
| ----------------------------------------- | -------------------------------- | -------- | -------- |
| [DL4001](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is moderate | Info     | No       |
| [DL4002](docs/analyzers/DL4001-DL4002.md) | Cognitive complexity is elevated | Warning  | No       |

### Performance (DL5xxx)

| Rule                               | Description                   | Severity | Code Fix |
| ---------------------------------- | ----------------------------- | -------- | -------- |
| [DL5001](docs/analyzers/DL5001.md) | Possible multiple enumeration | Warning  | Yes      |
| [DL5002](docs/analyzers/DL5002.md) | Use `string.IsNullOrEmpty`    | Warning  | Yes      |

See each rule's documentation for configuration options and attributes.

## Licence

This project is distributed under the [MIT NON-AI Licence](LICENSE).

For all practical intents, this operates exactly like the standard MIT licence. You have the freedom to use, modify, distribute, and commercialise the software. I genuinely want you to build useful things with it.

There is, however, one firm condition: **this code must not be used to train, enhance, or develop any artificial intelligence or machine learning models**. This prohibition is comprehensive, covering direct training, inclusion in datasets, and any derivative ML applications. As an independent developer, I do not have a legal team to enforce this; I am simply relying on your professional integrity and basic courtesy to respect the boundary.

Do note that because it restricts a specific field of endeavour, this isn't strictly an [OSI-approved](https://opensource.org/licenses) open-source licence. It is more accurately described as 'source-available'. The trade-off is straightforward: you keep all the usual freedoms you would expect from MIT, provided the source code is never ingested by an AI training pipeline.
