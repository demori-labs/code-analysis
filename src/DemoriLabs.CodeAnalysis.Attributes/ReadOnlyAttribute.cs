using System.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Attributes;

/// <summary>
/// Prevents reassignment of the annotated parameter within the method body.
/// Triggers <c>DL2001</c> on any reassignment attempt.
/// </summary>
[Conditional("DEMORILABS_DIAGNOSTICS_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ReadOnlyAttribute : Attribute;
