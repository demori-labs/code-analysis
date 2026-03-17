# DemoriLabs.CodeAnalysis

Roslyn analyzers and code fixes for C#, designed to enforce design patterns, catch common mistakes, and improve code quality.

## Rules

| Rule                                      | Category    | Severity | Code Fix | Description                                          |
| ----------------------------------------- | ----------- | -------- | -------- | ---------------------------------------------------- |
| [DL1001](docs/analyzers/DL1001.md)        | Design      | Warning  | Yes      | Records should not have mutable properties           |
| [DL1002](docs/analyzers/DL1002.md)        | Design      | Warning  | No       | Records should not have mutable property types       |
| [DL1003](docs/analyzers/DL1003.md)        | Design      | Warning  | Yes      | Record has too many positional parameters            |
| [DL1004](docs/analyzers/DL1004.md)        | Design      | Info     | Yes      | Data class could be a record                         |
| [DL2001](docs/analyzers/DL2001-DL2002.md) | Usage       | Error    | Yes      | Parameter must not be reassigned                     |
| [DL2002](docs/analyzers/DL2001-DL2002.md) | Usage       | Error    | Yes      | `[ReadOnly]` is incompatible with parameter modifier |
| [DL2003](docs/analyzers/DL2003.md)        | Usage       | Info     | Yes      | Primary constructor parameter should be `[ReadOnly]` |
| [DL3001](docs/analyzers/DL3001.md)        | Style       | Warning  | Yes      | Use named arguments                                  |
| [DL3002](docs/analyzers/DL3002.md)        | Style       | Info     | Yes      | Invert `if` statement to reduce nesting              |
| [DL4001](docs/analyzers/DL4001-DL4002.md) | Complexity  | Info     | No       | Cognitive complexity is moderate                     |
| [DL4002](docs/analyzers/DL4001-DL4002.md) | Complexity  | Warning  | No       | Cognitive complexity is elevated                     |
| [DL5001](docs/analyzers/DL5001.md)        | Performance | Warning  | Yes      | Possible multiple enumeration                        |

## Attributes

The package includes attributes that can be used to suppress diagnostics or configure analyser behaviour. They are compiled away via `[Conditional]` and have no runtime cost.

| Attribute                        | Rules                  | Purpose                                               |
| -------------------------------- | ---------------------- | ----------------------------------------------------- |
| `[Mutable]`                      | DL1001, DL1002, DL1004 | Suppress mutability diagnostics on records or classes |
| `[ReadOnly]`                     | DL2001, DL2002         | Prevent parameter reassignment                        |
| `[NamedArgument]`                | DL3001                 | Require named arguments at call sites                 |
| `[SuppressCognitiveComplexity]`  | DL4001, DL4002         | Suppress complexity diagnostics                       |
| `[CognitiveComplexityThreshold]` | DL4001, DL4002         | Override moderate/elevated thresholds                 |
| `[SuppressMultipleEnumeration]`  | DL5001                 | Allow multiple enumeration of a parameter             |

## Configuration

All rules support severity configuration via `.editorconfig`:

```ini
# Disable a rule
dotnet_diagnostic.DL1001.severity = none

# Change severity
dotnet_diagnostic.DL3001.severity = error
```

Some rules have additional options:

```ini
# DL1003: Set the positional parameter threshold (default: 4)
dotnet_diagnostic.DL1003.positional_parameters_threshold = 6
```

## Licence

MIT
