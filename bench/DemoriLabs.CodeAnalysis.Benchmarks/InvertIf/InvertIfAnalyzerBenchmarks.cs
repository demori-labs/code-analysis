using BenchmarkDotNet.Attributes;
using DemoriLabs.CodeAnalysis.InvertIf;
using Microsoft.CodeAnalysis.CSharp;

namespace DemoriLabs.CodeAnalysis.Benchmarks.InvertIf;

[MemoryDiagnoser]
public class InvertIfToReduceNestingAnalyzerBenchmarks
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
            using System.IO;

            // Simulates a legacy codebase with deeply nested ifs across many methods and classes.

            public class UserService
            {
                private readonly Dictionary<int, string> _users = new();
                private readonly List<string> _log = new();

                public void ProcessItem(string item)
                {
                    if (item != null)
                    {
                        if (item.Length > 0)
                        {
                            Console.WriteLine(item);
                            Console.WriteLine(item.ToUpper());
                        }
                    }
                }

                public int Calculate(int? value)
                {
                    if (value.HasValue)
                    {
                        var result = value.Value * 2;
                        return result + 1;
                    }

                    return 0;
                }

                public void AlreadyFlat(string input)
                {
                    if (string.IsNullOrEmpty(input))
                        return;

                    Console.WriteLine(input);
                }

                public string GetUserName(int id)
                {
                    if (_users.ContainsKey(id))
                    {
                        var name = _users[id];
                        if (name.Length > 0)
                        {
                            return name.Trim();
                        }

                        return "(empty)";
                    }

                    return "(unknown)";
                }

                public void RegisterUser(int id, string name)
                {
                    if (id > 0)
                    {
                        if (name != null)
                        {
                            if (name.Length <= 100)
                            {
                                _users[id] = name;
                                _log.Add($"Registered {name}");
                                Console.WriteLine($"User {id} registered.");
                            }
                        }
                    }
                }

                public void DeleteUser(int id)
                {
                    if (_users.ContainsKey(id))
                    {
                        var name = _users[id];
                        _users.Remove(id);
                        _log.Add($"Deleted {name}");
                        Console.WriteLine($"User {id} deleted.");
                    }
                }

                public void UpdateUser(int id, string newName)
                {
                    if (_users.ContainsKey(id))
                    {
                        if (newName != null)
                        {
                            if (newName != _users[id])
                            {
                                var old = _users[id];
                                _users[id] = newName;
                                _log.Add($"Updated {old} -> {newName}");
                            }
                        }
                    }
                }

                public int CountActiveUsers(List<int> ids)
                {
                    if (ids != null)
                    {
                        var count = 0;
                        foreach (var id in ids)
                        {
                            if (_users.ContainsKey(id))
                            {
                                count++;
                            }
                        }
                        return count;
                    }

                    return 0;
                }

                public void NotifyAll(Action<string> callback)
                {
                    if (callback != null)
                    {
                        foreach (var kvp in _users)
                        {
                            if (kvp.Value != null)
                            {
                                callback(kvp.Value);
                            }
                        }
                    }
                }
            }

            public class OrderProcessor
            {
                private readonly List<string> _orders = new();
                private readonly List<string> _errors = new();

                public bool ProcessOrder(string orderId, decimal amount, string currency)
                {
                    if (orderId != null)
                    {
                        if (amount > 0)
                        {
                            if (currency != null)
                            {
                                if (currency == "GBP" || currency == "USD" || currency == "EUR")
                                {
                                    _orders.Add(orderId);
                                    Console.WriteLine($"Processed {orderId}: {amount} {currency}");
                                    return true;
                                }

                                _errors.Add($"Unsupported currency: {currency}");
                                return false;
                            }

                            _errors.Add("Currency is null");
                            return false;
                        }

                        _errors.Add("Invalid amount");
                        return false;
                    }

                    _errors.Add("Order ID is null");
                    return false;
                }

                public void ProcessBatch(List<string> orderIds)
                {
                    if (orderIds != null)
                    {
                        if (orderIds.Count > 0)
                        {
                            foreach (var id in orderIds)
                            {
                                if (id != null)
                                {
                                    if (id.StartsWith("ORD-"))
                                    {
                                        _orders.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }

                public string GetOrderStatus(string orderId)
                {
                    if (orderId != null)
                    {
                        if (_orders.Contains(orderId))
                        {
                            return "processed";
                        }

                        return "not_found";
                    }

                    return "invalid";
                }

                public void CancelOrder(string orderId)
                {
                    if (orderId != null)
                    {
                        if (_orders.Contains(orderId))
                        {
                            _orders.Remove(orderId);
                            Console.WriteLine($"Cancelled {orderId}");
                        }
                    }
                }

                public decimal CalculateTotal(List<decimal> prices, decimal taxRate)
                {
                    if (prices != null)
                    {
                        if (prices.Count > 0)
                        {
                            if (taxRate >= 0)
                            {
                                var subtotal = 0m;
                                foreach (var price in prices)
                                {
                                    if (price > 0)
                                    {
                                        subtotal += price;
                                    }
                                }
                                return subtotal * (1 + taxRate);
                            }

                            return -1;
                        }

                        return 0;
                    }

                    return -1;
                }
            }

            public class FileProcessor
            {
                private readonly List<string> _processed = new();

                public void ProcessFiles(string[] paths)
                {
                    if (paths != null)
                    {
                        if (paths.Length > 0)
                        {
                            foreach (var path in paths)
                            {
                                if (path != null)
                                {
                                    if (path.EndsWith(".txt") || path.EndsWith(".csv"))
                                    {
                                        _processed.Add(path);
                                        Console.WriteLine($"Processed: {path}");
                                    }
                                }
                            }
                        }
                    }
                }

                public string ReadFirst(string[] paths)
                {
                    if (paths != null)
                    {
                        if (paths.Length > 0)
                        {
                            var first = paths[0];
                            if (first != null)
                            {
                                return first;
                            }

                            return "(null entry)";
                        }

                        return "(empty)";
                    }

                    return "(null)";
                }

                public void ProcessDirectory(string dirPath)
                {
                    if (dirPath != null)
                    {
                        if (dirPath.Length > 0)
                        {
                            if (Directory.Exists(dirPath))
                            {
                                var files = Directory.GetFiles(dirPath);
                                foreach (var file in files)
                                {
                                    if (file.EndsWith(".log"))
                                    {
                                        Console.WriteLine($"Log: {file}");
                                    }
                                }
                            }
                        }
                    }
                }

                public int CountExtension(string[] paths, string extension)
                {
                    if (paths != null)
                    {
                        if (extension != null)
                        {
                            var count = 0;
                            for (var i = 0; i < paths.Length; i++)
                            {
                                if (paths[i] != null)
                                {
                                    if (paths[i].EndsWith(extension))
                                    {
                                        count++;
                                    }
                                }
                            }
                            return count;
                        }

                        return -1;
                    }

                    return -1;
                }
            }

            public class DataValidator
            {
                public bool ValidateRecord(string name, int age, string email)
                {
                    if (name != null)
                    {
                        if (name.Length >= 2)
                        {
                            if (age > 0)
                            {
                                if (age < 150)
                                {
                                    if (email != null)
                                    {
                                        if (email.Contains("@"))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return false;
                }

                public string Classify(int score)
                {
                    if (score >= 0)
                    {
                        if (score < 100)
                        {
                            if (score >= 90)
                            {
                                return "A";
                            }

                            return score >= 80 ? "B" : "C";
                        }

                        return "invalid_high";
                    }

                    return "invalid_negative";
                }

                public void ValidateBatch(List<string> items)
                {
                    if (items != null)
                    {
                        for (var i = 0; i < items.Count; i++)
                        {
                            if (items[i] != null)
                            {
                                if (items[i].Length > 0)
                                {
                                    if (items[i].Length <= 256)
                                    {
                                        Console.WriteLine($"Valid: {items[i]}");
                                    }
                                }
                            }
                        }
                    }
                }

                public void ProcessWithSwitch(string type, string value)
                {
                    if (type != null)
                    {
                        if (value != null)
                        {
                            switch (type)
                            {
                                case "int":
                                    if (int.TryParse(value, out var intVal))
                                    {
                                        Console.WriteLine($"Int: {intVal}");
                                    }
                                    break;
                                case "bool":
                                    if (bool.TryParse(value, out var boolVal))
                                    {
                                        Console.WriteLine($"Bool: {boolVal}");
                                    }
                                    break;
                                default:
                                    Console.WriteLine($"String: {value}");
                                    break;
                            }
                        }
                    }
                }
            }

            public class CacheManager
            {
                private readonly Dictionary<string, object> _cache = new();
                private readonly Dictionary<string, DateTime> _expiry = new();

                public object Get(string key)
                {
                    if (key != null)
                    {
                        if (_cache.ContainsKey(key))
                        {
                            if (_expiry.ContainsKey(key))
                            {
                                if (_expiry[key] > DateTime.UtcNow)
                                {
                                    return _cache[key];
                                }

                                _cache.Remove(key);
                                _expiry.Remove(key);
                                return null;
                            }

                            return _cache[key];
                        }

                        return null;
                    }

                    return null;
                }

                public void Set(string key, object value, TimeSpan? ttl)
                {
                    if (key != null)
                    {
                        if (value != null)
                        {
                            _cache[key] = value;
                            if (ttl.HasValue)
                            {
                                _expiry[key] = DateTime.UtcNow.Add(ttl.Value);
                            }
                        }
                    }
                }

                public void Evict(string key)
                {
                    if (key != null)
                    {
                        if (_cache.ContainsKey(key))
                        {
                            _cache.Remove(key);
                            _expiry.Remove(key);
                            Console.WriteLine($"Evicted {key}");
                        }
                    }
                }

                public void Cleanup()
                {
                    var now = DateTime.UtcNow;
                    var keys = new List<string>(_expiry.Keys);
                    foreach (var key in keys)
                    {
                        if (_expiry.ContainsKey(key))
                        {
                            if (_expiry[key] <= now)
                            {
                                _cache.Remove(key);
                                _expiry.Remove(key);
                            }
                        }
                    }
                }

                public int CountValid()
                {
                    var count = 0;
                    var now = DateTime.UtcNow;
                    foreach (var kvp in _expiry)
                    {
                        if (kvp.Value > now)
                        {
                            count++;
                        }
                    }
                    return count;
                }
            }

            public class StringUtilities
            {
                public string TrimAndUpper(string input)
                {
                    if (input != null)
                    {
                        if (input.Length > 0)
                        {
                            var trimmed = input.Trim();
                            if (trimmed.Length > 0)
                            {
                                return trimmed.ToUpper();
                            }

                            return string.Empty;
                        }

                        return string.Empty;
                    }

                    return null;
                }

                public void PrintAll(IEnumerable<string> items)
                {
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (item != null)
                            {
                                if (item.Trim().Length > 0)
                                {
                                    Console.WriteLine(item);
                                }
                            }
                        }
                    }
                }

                public List<string> FilterNonEmpty(List<string> input)
                {
                    if (input != null)
                    {
                        var result = new List<string>();
                        foreach (var s in input)
                        {
                            if (s != null)
                            {
                                if (s.Length > 0)
                                {
                                    result.Add(s);
                                }
                            }
                        }
                        return result;
                    }

                    return new List<string>();
                }

                public string JoinNonNull(string separator, params string[] values)
                {
                    if (separator != null)
                    {
                        if (values != null)
                        {
                            if (values.Length > 0)
                            {
                                var parts = new List<string>();
                                foreach (var v in values)
                                {
                                    if (v != null)
                                    {
                                        parts.Add(v);
                                    }
                                }
                                return string.Join(separator, parts);
                            }

                            return string.Empty;
                        }

                        return string.Empty;
                    }

                    return string.Empty;
                }
            }
            """
        );
    }

    [Benchmark]
    public async Task Analyze()
    {
        await CompilationFactory.RunAnalyzerAsync(_compilation, new InvertIfToReduceNestingAnalyzer());
    }
}
