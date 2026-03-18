---
name: csharp-code-principles
description: C# code principles and modern idioms. Use when writing or modifying any C# code to ensure latest language features, pattern matching, and nullable correctness are applied.
user-invocable: false
---

# C# Code Principles

When writing or modifying C# code, always apply these principles.

## Pattern Matching

Prefer pattern matching over traditional checks:

- `is false` over `!` for boolean negation: `if (condition is false)` not `if (!condition)`
- `is null` / `is not null` over `== null` / `!= null`
- `is { Length: > 0 }` over `.Length > 0` when checking on a nullable type
- Use `switch` expressions over `switch` statements where the result is an assignment or return
- Use property patterns: `if (obj is { Name: "foo", Age: > 18 })` over chained `&&` conditions
- Use list patterns where applicable: `[var first, .., var last]`
- Use relational patterns: `is > 0 and < 100`
- Combine patterns with `and`, `or`, `not` instead of `&&`, `||`, `!` in pattern contexts

## C# 14 / .NET 10 Features

Always use the most modern syntax available. This project targets .NET 10 / C# 14.

### Extension Members

Use the new `extension` block syntax for extension properties, operators, and static members — not just methods:

```csharp
extension(string s)
{
    public bool IsNullOrEmpty => string.IsNullOrEmpty(s);
}
```

### `field` Keyword

Use the `field` contextual keyword in property accessors instead of declaring explicit backing fields:

```csharp
public string Name
{
    get;
    set => field = value ?? throw new ArgumentNullException(nameof(Name));
}
```

### Null-Conditional Assignment

Use `?.` on the left-hand side of assignments: `customer?.Order = GetOrder();`

### Other Modern Syntax

- Primary constructors on classes and structs where appropriate
- Collection expressions: `[1, 2, 3]` over `new[] { 1, 2, 3 }` or `new List<int> { 1, 2, 3 }`
- Raw string literals (`"""`) for multi-line strings or strings containing quotes
- `required` modifier on properties instead of constructor validation where suitable
- File-scoped namespaces (`namespace Foo;`) over block-scoped
- Target-typed `new()` where the type is clear from context
- Global and implicit usings (do not add redundant using statements)
- `readonly` on structs and struct members where possible
- Ranges and indices: `array[^1]`, `array[1..3]`

## Nullable Reference Types

This project has `<Nullable>enable</Nullable>` globally:

- Never suppress nullable warnings with `!` (null-forgiving operator) without a comment explaining why
- Use `is not null` checks to narrow nullable types (pattern matching, not `!= null`)
- Prefer `??` and `??=` for default values
- Use `[NotNullWhen]`, `[MaybeNullWhen]`, `[NotNull]` attributes to express nullability contracts on methods
- Mark return types as nullable (`T?`) when a method can legitimately return null
- Do not use `default!` to satisfy nullability — initialise properly or make the field nullable

## General

- Prefer `string.IsNullOrEmpty` / `string.IsNullOrWhiteSpace` with pattern matching: `if (value is null or "")`
- Prefer LINQ method syntax over query syntax
- Use `nameof()` instead of string literals for member references
- Use `const` or `static readonly` for values known at compile time
