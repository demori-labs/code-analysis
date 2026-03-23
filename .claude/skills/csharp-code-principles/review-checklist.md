# C# Code Review Checklist

Use this checklist when writing or reviewing any C# code in this project.

## Pattern matching

- [ ] `is null` / `is not null` used instead of `== null` / `!= null`
- [ ] `is false` used instead of `!` for boolean negation
- [ ] Type checks use `is Type variable` pattern, not `is Type` + cast
- [ ] Property patterns used over chained `&&` conditions where readable (max 3 properties)
- [ ] `switch` expressions used where the result is an assignment or return
- [ ] Relational patterns (`is > 0 and < 100`) used where clearer than comparison operators
- [ ] Logical patterns (`and`, `or`, `not`) used instead of `&&`, `||`, `!` in pattern contexts
- [ ] List patterns used where applicable (`[var first, .., var last]`)

## Modern syntax (C# 12-14 / .NET 10)

- [ ] File-scoped namespaces (`namespace Foo;`)
- [ ] Collection expressions (`[1, 2, 3]`) instead of `new[] { ... }` or `new List<T> { ... }` — explicit target type required (`var` doesn't work)
- [ ] Target-typed `new()` for fields, properties, and parameters — not for locals (use `var` with full constructor)
- [ ] Raw string literals (`"""`) for multi-line strings or strings with quotes
- [ ] Primary constructors on classes/structs where appropriate (DI, simple initialisation)
- [ ] `required` modifier on properties instead of constructor validation where suitable
- [ ] Ranges and indices (`array[^1]`, `array[1..3]`) instead of `.Length - 1` arithmetic
- [ ] `using` declarations (no braces) instead of `using` blocks where scope is clear
- [ ] `field` keyword in property accessors instead of explicit backing fields (C# 14)
- [ ] Null-conditional assignment (`obj?.Prop = value`) instead of `if (obj is not null)` guards (C# 14)
- [ ] Extension members (extension blocks) for new extension properties/operators (C# 14)
- [ ] `params ReadOnlySpan<T>` or `params Span<T>` instead of `params T[]` for new APIs (C# 13)
- [ ] `System.Threading.Lock` instead of `object` for lock targets (C# 13)
- [ ] Lambda parameter modifiers without explicit types (`(text, out result) => ...`) (C# 14)

## Nullable reference types

- [ ] No suppression with `!` (null-forgiving operator) without a justifying comment
- [ ] `is not null` used to narrow nullable types, not `!= null`
- [ ] `??` and `??=` used for default values
- [ ] Nullability attributes (`[NotNullWhen]`, `[MaybeNullWhen]`, `[NotNull]`) used on public API boundaries
- [ ] Return types marked as `T?` when the method can legitimately return null
- [ ] No `default!` to satisfy nullability — initialise properly or make the field/property nullable

## String comparison

- [ ] `StringComparison` specified explicitly on all `string.Equals`, `string.Compare`, `StartsWith`, `EndsWith`, `Contains`, `IndexOf` calls — never rely on defaults
- [ ] `StringComparison.Ordinal` or `OrdinalIgnoreCase` used for non-linguistic comparisons (identifiers, keys, paths, protocols)
- [ ] `StringComparison.CurrentCulture` used only for user-facing display/sorting
- [ ] `StringComparison.InvariantCulture` avoided unless persisting linguistically relevant cross-cultural data
- [ ] `string.Equals` used for equality checks, not `string.Compare(...) == 0`
- [ ] `ToUpperInvariant()` used over `ToLowerInvariant()` when normalising for comparison
- [ ] `string.IsNullOrEmpty()` / `string.IsNullOrWhiteSpace()` used for null/empty checks — not pattern matching (`is null or ""`)
- [ ] `Dictionary<string, T>` and `HashSet<string>` use explicit `StringComparer` (e.g. `StringComparer.OrdinalIgnoreCase`)

## String formatting

- [ ] String interpolation (`$"..."`) over concatenation (`+`)
- [ ] `StringBuilder` used for string building in loops
- [ ] Raw string literals over escape sequences or verbatim strings for multi-line content
- [ ] `nameof()` over string literals for member references

## General

- [ ] `const` or `static readonly` for values known at compile time
- [ ] LINQ method syntax preferred over query syntax
- [ ] `var` used for all local variable declarations
- [ ] Global/implicit usings leveraged — no redundant `using` statements
- [ ] `readonly` on structs and struct members where possible
- [ ] `Func<>` / `Action<>` used instead of defining custom delegate types (unless needed for clarity)
- [ ] `sealed` on classes not designed for inheritance
