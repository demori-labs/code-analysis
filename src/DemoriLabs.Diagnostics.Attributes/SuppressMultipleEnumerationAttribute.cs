using System.Diagnostics;

namespace DemoriLabs.Diagnostics.Attributes;

/// <summary>
/// Suppresses the multiple enumeration diagnostic (<c>DL5001</c>) for the annotated parameter.
/// </summary>
[Conditional("DEMORILABS_DIAGNOSTICS_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SuppressMultipleEnumerationAttribute : Attribute;
