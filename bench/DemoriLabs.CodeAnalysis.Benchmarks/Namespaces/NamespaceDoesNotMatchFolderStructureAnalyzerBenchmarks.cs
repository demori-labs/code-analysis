using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Benchmarks.Namespaces;

[MemoryDiagnoser]
public class NamespaceDoesNotMatchFolderStructureAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;
    private AnalyzerOptions _options = null!;

    [GlobalSetup]
    public void Setup()
    {
        var sources = new (string path, string source)[]
        {
            (
                "/src/MyProject/Models/User.cs",
                """
                namespace MyProject.Models;

                public class User
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                    public string Email { get; set; } = "";
                }
                """
            ),
            (
                "/src/MyProject/Models/Order.cs",
                """
                namespace MyProject.Models;

                public class Order
                {
                    public int Id { get; set; }
                    public decimal Amount { get; set; }
                    public string Currency { get; set; } = "GBP";
                }
                """
            ),
            (
                "/src/MyProject/Services/UserService.cs",
                """
                namespace MyProject.Services;

                public class UserService
                {
                    public void CreateUser(string name, string email) { }
                    public void DeleteUser(int id) { }
                    public void UpdateUser(int id, string name) { }
                }
                """
            ),
            (
                "/src/MyProject/Services/OrderService.cs",
                """
                namespace MyProject.Services;

                public class OrderService
                {
                    public void ProcessOrder(int orderId) { }
                    public void CancelOrder(int orderId) { }
                }
                """
            ),
            (
                "/src/MyProject/Services/Handlers/OrderHandler.cs",
                """
                namespace MyProject.Services.Handlers;

                public class OrderHandler
                {
                    public void Handle() { }
                }
                """
            ),
            (
                "/src/MyProject/Controllers/UserController.cs",
                """
                namespace MyProject.Controllers;

                public class UserController
                {
                    public void Get() { }
                    public void Post() { }
                    public void Put() { }
                    public void Delete() { }
                }
                """
            ),
            (
                "/src/MyProject/Controllers/OrderController.cs",
                """
                namespace MyProject.Controllers;

                public class OrderController
                {
                    public void Get() { }
                    public void Post() { }
                }
                """
            ),
            (
                "/src/MyProject/Data/Repository.cs",
                """
                namespace MyProject.Data;

                public class Repository<T>
                {
                    public void Add(T entity) { }
                    public void Remove(T entity) { }
                }
                """
            ),
            (
                "/src/MyProject/WrongNamespace.cs",
                """
                namespace SomethingElse;

                public class WrongNamespace
                {
                    public void DoSomething() { }
                }
                """
            ),
            (
                "/src/MyProject/Models/Entities/Product.cs",
                """
                namespace MyProject.Models;

                public class Product
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }
                """
            ),
            (
                "/src/MyProject/Program.cs",
                """
                namespace MyProject;

                public static class Program
                {
                    public static void Main() { }
                }
                """
            ),
            (
                "/src/MyProject/Utilities/StringExtensions.cs",
                """
                namespace MyProject.Utilities;

                public static class StringExtensions
                {
                    public static string Truncate(this string s, int length) => s.Length <= length ? s : s.Substring(0, length);
                }
                """
            ),
        };

        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s.source, path: s.path)).ToArray();

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

        _compilation = CSharpCompilation.Create(
            "MyProject",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var configText = """
            is_global = true
            build_property.RootNamespace = MyProject
            build_property.ProjectDir = /src/MyProject/
            """;

        var optionsProvider = new GlobalConfigOptionsProvider(configText);
        _options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, optionsProvider);
    }

    [Benchmark]
    public async Task Analyze()
    {
        var withAnalyzers = _compilation.WithAnalyzers([new NamespaceDoesNotMatchFolderStructureAnalyzer()], _options);
        await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class GlobalConfigOptionsProvider(string configText) : AnalyzerConfigOptionsProvider
    {
        private readonly GlobalConfigOptions _globalOptions = new(configText);

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions.Instance;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions.Instance;

        private sealed class GlobalConfigOptions(string configText) : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _values = ParseConfig(configText);

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
                _values.TryGetValue(key, out value!);

            private static Dictionary<string, string> ParseConfig(string text)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    var key = trimmed.Substring(0, eqIndex).Trim();
                    var value = trimmed.Substring(eqIndex + 1).Trim();
                    result[key] = value;
                }

                return result;
            }
        }

        private sealed class EmptyOptions : AnalyzerConfigOptions
        {
            public static readonly EmptyOptions Instance = new();

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = null;
                return false;
            }
        }
    }
}
