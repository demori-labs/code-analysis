using System.Diagnostics;

namespace DemoriLabs.Diagnostics.Attributes;

/// <summary>
/// Marks a record or property as intentionally mutable, suppressing
/// <c>DL1001</c> and <c>DL1002</c> diagnostics.
/// </summary>
[Conditional("DEMORILABS_DIAGNOSTICS_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property)]
public sealed class MutableAttribute : Attribute;
