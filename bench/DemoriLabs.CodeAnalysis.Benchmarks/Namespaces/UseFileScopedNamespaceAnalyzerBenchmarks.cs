using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.Namespaces;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.Namespaces;

[MemoryDiagnoser]
public class UseFileScopedNamespaceAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            namespace MyApp
            {
                public class UserService
                {
                    public void CreateUser(string name) { }
                    public void DeleteUser(int id) { }
                }

                public class OrderService
                {
                    public void ProcessOrder(int orderId) { }
                    public void CancelOrder(int orderId) { }
                }

                public interface IRepository<T>
                {
                    void Add(T entity);
                    void Remove(T entity);
                    T? GetById(int id);
                }
            }

            namespace MyApp.Models
            {
                public class User
                {
                    public int Id { get; set; }
                    public string Name { get; set; } = "";
                }

                public class Order
                {
                    public int Id { get; set; }
                    public decimal Amount { get; set; }
                }

                public record Product(int Id, string Name, decimal Price);
            }

            namespace MyApp.Data
            {
                public class DbContext
                {
                    public void SaveChanges() { }
                }
            }

            namespace MyApp.Data
            {
                public class Repository
                {
                    public void Add(object entity) { }
                }
            }

            namespace Correct;

            public class AlreadyFileScoped { }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new UseFileScopedNamespaceAnalyzer());
    }
}
