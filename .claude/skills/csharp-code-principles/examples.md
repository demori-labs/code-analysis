# C# Modern Idioms — Examples

Concrete before/after examples for every principle. When writing or reviewing C# code, apply the **After** pattern.

## Pattern Matching

### Null checks

```csharp
// Before
if (value == null) { ... }
if (value != null) { ... }

// After
if (value is null) { ... }
if (value is not null) { ... }
```

### Boolean negation

```csharp
// Before
if (!condition) { ... }
if (!string.IsNullOrEmpty(name)) { ... }

// After
if (condition is false) { ... }
if (string.IsNullOrEmpty(name) is false) { ... }
```

### Type checks and casts

```csharp
// Before
if (obj is string)
{
    var s = (string)obj;
    Console.WriteLine(s.Length);
}

// After
if (obj is string s)
{
    Console.WriteLine(s.Length);
}
```

### Property patterns over chained conditions

```csharp
// Before
if (person != null && person.Name == "Alice" && person.Age > 18) { ... }

// After
if (person is { Name: "Alice", Age: > 18 }) { ... }
```

### Nullable type member access

```csharp
// Before
if (list != null && list.Count > 0) { ... }

// After
if (list is { Count: > 0 }) { ... }
```

### Relational and logical patterns

```csharp
// Before
if (score >= 0 && score < 100) { ... }

// After
if (score is >= 0 and < 100) { ... }
```

### Switch expressions over switch statements

```csharp
// Before
string label;
switch (status)
{
    case Status.Active:
        label = "Active";
        break;
    case Status.Inactive:
        label = "Inactive";
        break;
    default:
        label = "Unknown";
        break;
}

// After
var label = status switch
{
    Status.Active => "Active",
    Status.Inactive => "Inactive",
    _ => "Unknown",
};
```

### List patterns

```csharp
// Before
if (items.Length >= 2 && items[0] == "header")
{
    var last = items[items.Length - 1];
}

// After
if (items is ["header", .., var last])
{
    // use last
}
```

### Combining patterns

```csharp
// Before
if (!(status == Status.Pending || status == Status.Draft)) { ... }

// After
if (status is not (Status.Pending or Status.Draft)) { ... }
```

## Collections and Initialisation

### Collection expressions

Collection expressions require an explicit target type — `var` cannot infer from `[...]`.

```csharp
// Before
var numbers = new List<int> { 1, 2, 3 };
var array = new int[] { 1, 2, 3 };
var empty = Array.Empty<string>();
var combined = first.Concat(second).ToList();

// After — explicit type required (var doesn't work with collection expressions)
List<int> numbers = [1, 2, 3];
int[] array = [1, 2, 3];
string[] empty = [];
List<string> combined = [.. first, .. second];
```

### Target-typed new

For locals, use `var` with the full constructor. Target-typed `new()` is for fields, properties, and parameters where the type is already declared.

```csharp
// Locals — use var
var map = new Dictionary<string, List<int>>();

// Fields/properties — use target-typed new()
private readonly Dictionary<string, List<int>> _map = new();
public List<string> Items { get; } = new();
```

### Ranges and indices

```csharp
// Before
var last = array[array.Length - 1];
var slice = array.Skip(1).Take(3).ToArray();

// After
var last = array[^1];
var slice = array[1..4];
```

## Constructors and Initialisation

### Primary constructors (classes/structs)

```csharp
// Before
public class UserService
{
    private readonly ILogger _logger;
    private readonly IUserRepository _repo;

    public UserService(ILogger logger, IUserRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }
}

// After
public class UserService(ILogger logger, IUserRepository repo)
{
    // logger and repo are available throughout the class
}
```

### Required properties over constructor validation

```csharp
// Before
public class Config
{
    public string ConnectionString { get; }

    public Config(string connectionString)
    {
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }
}

// After
public class Config
{
    public required string ConnectionString { get; init; }
}
```

## C# 14 Features

### Extension members

```csharp
// Before (C# 13 and earlier)
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? s) => string.IsNullOrEmpty(s);
}

// After (C# 14) — extension properties and static members
public static class StringExtensions
{
    extension(string? s)
    {
        public bool IsNullOrEmpty => string.IsNullOrEmpty(s);
    }
}
```

### Field keyword

```csharp
// Before
private string _name = "";
public string Name
{
    get => _name;
    set => _name = value ?? throw new ArgumentNullException(nameof(value));
}

// After
public string Name
{
    get;
    set => field = value ?? throw new ArgumentNullException(nameof(value));
}
```

### Null-conditional assignment

```csharp
// Before
if (customer is not null)
{
    customer.Order = GetCurrentOrder();
}

// After
customer?.Order = GetCurrentOrder();
```

### Lambda parameter modifiers without types

```csharp
// Before
TryParse<int> parse = (string text, out int result) => int.TryParse(text, out result);

// After
TryParse<int> parse = (text, out result) => int.TryParse(text, out result);
```

### Unbound generics in nameof

```csharp
// Before
var name = nameof(Dictionary<object, object>); // "Dictionary"

// After
var name = nameof(Dictionary<,>); // "Dictionary"
```

## C# 13 Features

### params collections (not just arrays)

```csharp
// Before — params only works with arrays
public void Log(params string[] messages) { ... }

// After — params with Span, ReadOnlySpan, IEnumerable, etc.
public void Log(params ReadOnlySpan<string> messages) { ... }
```

### New Lock type

```csharp
// Before
private readonly object _lock = new();
lock (_lock) { ... }

// After
private readonly Lock _lock = new();
lock (_lock) { ... } // uses Lock.EnterScope() — more efficient
```

### Partial properties

```csharp
// Declaring declaration
public partial class ViewModel
{
    public partial string Name { get; set; }
}

// Implementing declaration (e.g. source-generated)
public partial class ViewModel
{
    private string _name = "";
    public partial string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
}
```

## Nullable Reference Types

### Null narrowing with pattern matching

```csharp
// Before
if (result != null)
{
    Console.WriteLine(result.ToString());
}

// After
if (result is { } value)
{
    Console.WriteLine(value.ToString());
}
// Or simply:
if (result is not null)
{
    Console.WriteLine(result.ToString());
}
```

### Null coalescing

```csharp
// Before
if (name == null)
{
    name = "default";
}

// After
name ??= "default";
```

### Nullability attributes

```csharp
// Express contracts — don't suppress with !
public bool TryGetValue(string key, [NotNullWhen(true)] out string? value) { ... }
public string GetRequired([NotNull] string? input) { ... }
```

## String Comparison

Always specify `StringComparison` explicitly. See [Microsoft best practices](https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings).

### Equality — use string.Equals with explicit comparison

```csharp
// Before — relies on default (culture-sensitive) comparison
if (string.Equals(url.Scheme, "https")) { ... }
if (name == "admin") { ... }

// After — explicit ordinal comparison
if (string.Equals(url.Scheme, "https", StringComparison.OrdinalIgnoreCase)) { ... }
if (string.Equals(name, "admin", StringComparison.Ordinal)) { ... }
```

### Choosing the right StringComparison

```csharp
// Internal identifiers, keys, protocol strings, file paths → Ordinal
string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase)
path.StartsWith("/api/", StringComparison.Ordinal)
name.Contains("test", StringComparison.OrdinalIgnoreCase)

// Data displayed to the user → CurrentCulture
string.Compare(displayName1, displayName2, StringComparison.CurrentCulture)

// Avoid InvariantCulture for non-linguistic comparisons — use Ordinal instead
```

### Sorting — use string.Compare, not Equals

```csharp
// Before — using Compare to check equality (wrong)
if (string.Compare(a, b) == 0) { ... }

// After — Equals for equality, Compare for sorting
if (string.Equals(a, b, StringComparison.Ordinal)) { ... }
Array.Sort(names, StringComparer.OrdinalIgnoreCase); // sorting
```

### Normalising case — ToUpperInvariant over ToLowerInvariant

```csharp
// Before
var normalised = value.ToLowerInvariant();

// After
var normalised = value.ToUpperInvariant();
```

### Null/empty checks — use dedicated methods

```csharp
// Before — pattern matching on strings
if (value is null or "") { ... }

// After — dedicated API, clear intent
if (string.IsNullOrEmpty(value)) { ... }
if (string.IsNullOrWhiteSpace(value)) { ... }
```

### Dictionary/HashSet with string keys

```csharp
// Before — default comparer (ordinal, case-sensitive)
var map = new Dictionary<string, int>();

// After — explicit comparer matching the use case
var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var set = new HashSet<string>(StringComparer.Ordinal);
```

## String Formatting

### Raw string literals

```csharp
// Before
var json = "{\n  \"name\": \"Alice\",\n  \"age\": 30\n}";
// or
var json = @"{
  ""name"": ""Alice"",
  ""age"": 30
}";

// After
var json = """
    {
      "name": "Alice",
      "age": 30
    }
    """;
```

### String interpolation over concatenation

```csharp
// Before
var message = "Hello, " + name + "! You have " + count + " messages.";

// After
var message = $"Hello, {name}! You have {count} messages.";
```

### StringBuilder for loops

```csharp
// Before
var result = "";
for (var i = 0; i < 1000; i++)
    result += items[i];

// After
var sb = new StringBuilder();
for (var i = 0; i < 1000; i++)
    sb.Append(items[i]);
var result = sb.ToString();
```

## General Idioms

### File-scoped namespaces

```csharp
// Before
namespace MyApp.Services
{
    public class UserService { ... }
}

// After
namespace MyApp.Services;

public class UserService { ... }
```

### Using declarations (no braces)

```csharp
// Before
using (var stream = File.OpenRead(path))
{
    // ...
}

// After
using var stream = File.OpenRead(path);
// disposed at end of scope
```

### nameof over string literals

```csharp
// Before
throw new ArgumentNullException("value");

// After
throw new ArgumentNullException(nameof(value));
```

### const and static readonly

```csharp
// Before
private string Separator = ";";

// After
private const string Separator = ";";
// or if not a compile-time constant:
private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
```

### LINQ method syntax over query syntax

```csharp
// Before (query syntax)
var adults = from p in people
             where p.Age >= 18
             orderby p.Name
             select p;

// After (method syntax)
var adults = people
    .Where(p => p.Age >= 18)
    .OrderBy(p => p.Name);
```

### Always use var

```csharp
// Before
List<string> items = new List<string>();
string name = "Alice";
int count = GetItemCount();

// After — always var, regardless of whether the type is obvious
var items = new List<string>();
var name = "Alice";
var count = GetItemCount();
```
