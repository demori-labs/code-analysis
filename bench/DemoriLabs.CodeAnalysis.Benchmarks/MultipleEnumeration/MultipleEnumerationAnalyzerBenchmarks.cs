using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.MultipleEnumeration;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.MultipleEnumeration;

[MemoryDiagnoser]
public class MultipleEnumerationAnalyzerBenchmarks
{
    private CSharpCompilation _compilation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _compilation = CompilationFactory.CreateCompilation(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq;

            public class OrderService
            {
                private readonly Dictionary<string, decimal> _cache = new();
                private readonly List<string> _log = new();

                // Should detect: two enumerations (Count + Sum)
                public void ProcessOrders(IEnumerable<decimal> amounts)
                {
                    var count = amounts.Count();
                    var total = amounts.Sum();
                    Console.WriteLine($"Processed {count} orders totalling {total:C}");
                }

                // Should detect: foreach + LINQ method
                public void AuditItems(IEnumerable<string> items)
                {
                    foreach (var item in items)
                    {
                        Console.WriteLine($"Auditing: {item}");
                    }
                    var sorted = items.OrderBy(x => x).ToList();
                    Console.WriteLine($"Sorted {sorted.Count} items");
                }

                // Should detect: three enumerations on local variable
                public void AnalyseData()
                {
                    IEnumerable<int> data = Enumerable.Range(0, 100);
                    var min = data.Min();
                    var max = data.Max();
                    var avg = data.Average();
                    Console.WriteLine($"Min={min}, Max={max}, Avg={avg}");
                }

                // Should detect: constructor with two enumerations
                public OrderService(IEnumerable<string> initialItems)
                {
                    var count = initialItems.Count();
                    var first = initialItems.First();
                    Console.WriteLine($"Initialised with {count} items, first: {first}");
                }

                // Should detect: multiple IEnumerable params, both enumerated twice
                public void MergeStreams(IEnumerable<int> left, IEnumerable<int> right, List<int> output)
                {
                    var leftCount = left.Count();
                    var leftSum = left.Sum();
                    var rightCount = right.Count();
                    var rightSum = right.Sum();
                    output.AddRange(new[] { leftCount, leftSum, rightCount, rightSum });
                }

                // No diagnostic: single enumeration
                public void SinglePass(IEnumerable<string> names)
                {
                    foreach (var name in names)
                    {
                        _log.Add(name);
                    }
                }

                // No diagnostic: chained LINQ is one enumeration
                public List<string> TransformPipeline(IEnumerable<string> raw)
                {
                    return raw.Where(x => x.Length > 0)
                        .Select(x => x.Trim())
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();
                }

                // No diagnostic: concrete types (IReadOnlyList, List, array)
                public void ConcreteCollections(IReadOnlyList<int> readOnly, List<string> list, int[] array)
                {
                    var a = readOnly.Count;
                    var b = readOnly[0];
                    var c = list.Count;
                    list.Add("x");
                    var d = array.Length;
                    var e = array[0];
                    Console.WriteLine($"{a},{b},{c},{d},{e}");
                }

                // No diagnostic: enumeration only inside nested lambda
                public void NestedScopes(IEnumerable<int> values)
                {
                    var materialised = values.ToList();
                    Action doWork = () =>
                    {
                        foreach (var v in values)
                        {
                            Console.WriteLine(v);
                        }
                    };
                    doWork();
                }

                // No diagnostic: no IEnumerable involvement at all
                public decimal CalculateDiscount(decimal subtotal, int customerTier)
                {
                    var rate = customerTier switch
                    {
                        1 => 0.05m,
                        2 => 0.10m,
                        3 => 0.15m,
                        _ => 0m,
                    };
                    var discount = subtotal * rate;
                    var minDiscount = Math.Min(discount, 50m);
                    return Math.Max(minDiscount, 0m);
                }

                // No diagnostic: no IEnumerable involvement, many statements
                public string FormatReport(string title, int year, bool includeHeader)
                {
                    var sb = new System.Text.StringBuilder();
                    if (includeHeader)
                    {
                        sb.AppendLine("=== REPORT ===");
                        sb.AppendLine($"Title: {title}");
                        sb.AppendLine($"Year: {year}");
                        sb.AppendLine();
                    }
                    for (var i = 0; i < 10; i++)
                    {
                        sb.AppendLine($"Line {i}: data");
                    }
                    sb.AppendLine("=== END ===");
                    return sb.ToString();
                }

                // No diagnostic: no IEnumerable involvement, dictionary and string ops
                public void UpdateCache(string key, string rawValue)
                {
                    var trimmed = rawValue.Trim();
                    var upper = trimmed.ToUpperInvariant();
                    if (_cache.ContainsKey(key))
                    {
                        _cache[key] = decimal.Parse(upper);
                    }
                    else
                    {
                        _cache.Add(key, decimal.Parse(upper));
                    }
                    _log.Add($"Updated {key}");
                }

                // No diagnostic: no IEnumerable involvement, arithmetic
                public (double X, double Y) ComputeCoordinates(double angle, double radius)
                {
                    var radians = angle * Math.PI / 180.0;
                    var x = radius * Math.Cos(radians);
                    var y = radius * Math.Sin(radians);
                    var magnitude = Math.Sqrt(x * x + y * y);
                    Console.WriteLine($"Magnitude: {magnitude}");
                    return (Math.Round(x, 4), Math.Round(y, 4));
                }

                // No diagnostic: no IEnumerable involvement, date logic
                public string DescribePeriod(DateTime start, DateTime end)
                {
                    var span = end - start;
                    var days = span.TotalDays;
                    var weeks = Math.Floor(days / 7);
                    var remaining = days % 7;
                    return $"{weeks} weeks and {remaining} days";
                }

                // No diagnostic: List<T> passed around, not IEnumerable
                public void ProcessList(List<int> data)
                {
                    var filtered = data.Where(x => x > 0).ToList();
                    var sorted = filtered.OrderBy(x => x).ToArray();
                    var grouped = data.GroupBy(x => x % 2).ToDictionary(g => g.Key, g => g.Count());
                    Console.WriteLine(string.Join(", ", sorted));
                    Console.WriteLine(grouped.Count);
                    Console.WriteLine(data.Sum());
                    Console.WriteLine(data.Average());
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new MultipleEnumerationAnalyzer());
    }
}
