using System.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Attributes;

/// <summary>
/// Marks a record, property, or primary constructor parameter as intentionally mutable,
/// suppressing <c>DL1001</c>, <c>DL1002</c>, and <c>DL2003</c> diagnostics.
/// </summary>
[Conditional("DEMORILABS_CODE_ANALYSIS")]
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Parameter
)]
public sealed class MutableAttribute : Attribute;
