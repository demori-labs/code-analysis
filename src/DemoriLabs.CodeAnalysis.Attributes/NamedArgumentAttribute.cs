using System.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Attributes;

/// <summary>
/// Requires callers to always use a named argument at the call site.
/// Enforces the <c>DL3001</c> diagnostic.
/// </summary>
[Conditional("DEMORILABS_CODE_ANALYSIS")]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class NamedArgumentAttribute : Attribute;
