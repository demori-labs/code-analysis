using System.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Attributes;

/// <summary>
/// Suppresses the cognitive complexity diagnostics (<c>DL4001</c> and <c>DL4002</c>) for the annotated member
/// or all methods within the annotated type.
/// </summary>
[Conditional("DEMORILABS_CODE_ANALYSIS")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SuppressCognitiveComplexityAttribute : Attribute;
