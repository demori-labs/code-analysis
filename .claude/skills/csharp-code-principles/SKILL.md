---
name: csharp-code-principles
description: C# code principles and modern idioms. Use when writing or modifying any C# code to ensure latest language features, pattern matching, and nullable correctness are applied.
user-invocable: false
---

# C# Code Principles

When writing or modifying C# code, always apply these principles. This project targets **.NET 10 / C# 14** — always use the most modern syntax available.

For concrete before/after examples, see [examples.md](examples.md).
For a review checklist, see [review-checklist.md](review-checklist.md).

## Pattern Matching

Prefer pattern matching over traditional checks:

- `is false` over `!` for boolean negation: `if (condition is false)` not `if (!condition)`
- `is null` / `is not null` over `== null` / `!= null`
- `is { Length: > 0 }` over `.Length > 0` when checking on a nullable type
- `switch` expressions over `switch` statements where the result is an assignment or return
- Property patterns: `if (obj is { Name: "foo", Age: > 18 })` over chained `&&` conditions — keep to max 3 properties for readability
- List patterns where applicable: `[var first, .., var last]`
- Relational patterns: `is > 0 and < 100`
- Combine patterns with `and`, `or`, `not` instead of `&&`, `||`, `!` in pattern contexts

## C# 14 Features

### Extension Members

Use the new `extension` block syntax for extension properties, operators, and static members — not just methods:

```csharp
public static class StringExtensions
{
    extension(string? s)
    {
        public bool IsNullOrEmpty => string.IsNullOrEmpty(s);
    }
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

### Lambda Parameter Modifiers

Add modifiers (`out`, `ref`, `in`, `scoped`) to lambda parameters without specifying types:

```csharp
TryParse<int> parse = (text, out result) => int.TryParse(text, out result);
```

### Unbound Generics in nameof

Use `nameof(Dictionary<,>)` instead of `nameof(Dictionary<object, object>)`.

## C# 13 Features

- **`params` collections**: Use `params ReadOnlySpan<T>` or `params Span<T>` instead of `params T[]` for new APIs — avoids heap allocation
- **`System.Threading.Lock`**: Use the new `Lock` type instead of `lock (object)` — more efficient via `Lock.EnterScope()`
- **Partial properties/indexers**: Use for source-generator scenarios
- **`ref struct` interfaces**: `ref struct` types can now implement interfaces (with restrictions)
- **`allows ref struct`**: Generic constraint enabling `Span<T>` as a type argument

## C# 12 and Earlier (Baseline)

These are established features that should always be used:

- **Primary constructors** on classes and structs where appropriate (DI, simple initialisation)
- **Collection expressions**: `[1, 2, 3]` over `new[] { 1, 2, 3 }` or `new List<int> { 1, 2, 3 }` — requires explicit target type (`var` doesn't work with `[...]`)
- **Raw string literals** (`"""`) for multi-line strings or strings containing quotes
- **`required` modifier** on properties instead of constructor validation where suitable
- **File-scoped namespaces** (`namespace Foo;`) over block-scoped
- **Target-typed `new()`** for fields, properties, and parameters — for locals, use `var` with the full constructor instead
- **Global and implicit usings** — do not add redundant using statements
- **`readonly`** on structs and struct members where possible
- **Ranges and indices**: `array[^1]`, `array[1..3]`
- **`using` declarations** (no braces) instead of `using` blocks

## Nullable Reference Types

This project has `<Nullable>enable</Nullable>` globally:

- Never suppress nullable warnings with `!` (null-forgiving operator) without a comment explaining why
- Use `is not null` checks to narrow nullable types (pattern matching, not `!= null`)
- Prefer `??` and `??=` for default values
- Use `[NotNullWhen]`, `[MaybeNullWhen]`, `[NotNull]` attributes to express nullability contracts on methods
- Mark return types as nullable (`T?`) when a method can legitimately return null
- Do not use `default!` to satisfy nullability — initialise properly or make the field nullable

## String Handling

Follow [Microsoft's best practices for comparing strings](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings):

- **Always specify `StringComparison` explicitly** — never rely on default overloads. The intent must be clear from the call site
- **Use `StringComparison.Ordinal`** for internal identifiers, keys, protocol strings, file paths — anything non-linguistic
- **Use `StringComparison.OrdinalIgnoreCase`** for case-insensitive non-linguistic comparisons (most common case)
- **Use `StringComparison.CurrentCulture`** only when displaying or sorting data shown to the user
- **Avoid `StringComparison.InvariantCulture`** in most cases — use `Ordinal` instead for non-linguistic data
- **Use `string.Equals(a, b, StringComparison.Ordinal)`** for equality — not `string.Compare` (which is for sorting)
- **Use `ToUpperInvariant()`** over `ToLowerInvariant()` when normalising strings for comparison
- Use `string.IsNullOrEmpty()` / `string.IsNullOrWhiteSpace()` for null/empty checks
- Use `StringBuilder` for string building in loops, string interpolation for simple concatenation
- Use raw string literals (`"""`) for multi-line or quote-heavy strings

## General

- Prefer LINQ method syntax over query syntax
- Use `nameof()` instead of string literals for member references
- Use `const` or `static readonly` for values known at compile time
- Always use `var` for local variable declarations
- Use `sealed` on classes not designed for inheritance
- **No comments** — code should be self-explanatory. If logic is unclear, refactor rather than comment
- **No `#region` blocks** — they hide code and discourage cohesion
