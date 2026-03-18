using System.Diagnostics;

namespace DemoriLabs.CodeAnalysis.Attributes;

/// <summary>
/// Overrides the cognitive complexity thresholds for the annotated member
/// or all methods within the annotated type.
/// <para>
/// When complexity exceeds <see cref="ModerateThreshold"/>, <c>DL4001</c> is reported as informational.
/// When complexity exceeds <see cref="ElevatedThreshold"/>, <c>DL4002</c> is reported as a warning instead.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Override the moderate threshold only
/// [CognitiveComplexityThreshold(moderateThreshold: 12)]
///
/// // Override both thresholds
/// [CognitiveComplexityThreshold(moderateThreshold: 10, elevatedThreshold: 25)]
/// </code>
/// </example>
/// <param name="moderateThreshold">
/// The complexity score above which <c>DL4001</c> (informational) is reported.
/// Defaults to <c>7</c>.
/// </param>
/// <param name="elevatedThreshold">
/// The complexity score above which <c>DL4002</c> (warning) is reported instead of <c>DL4001</c>.
/// Defaults to <c>15</c>.
/// </param>
[Conditional("DEMORILABS_DIAGNOSTICS_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class CognitiveComplexityThresholdAttribute(int moderateThreshold = 7, int elevatedThreshold = 15)
    : Attribute
{
    /// <summary>
    /// The complexity score above which <c>DL4001</c> (informational) is reported.
    /// </summary>
    public int ModerateThreshold { get; } = moderateThreshold;

    /// <summary>
    /// The complexity score above which <c>DL4002</c> (warning) is reported instead of <c>DL4001</c>.
    /// </summary>
    public int ElevatedThreshold { get; } = elevatedThreshold;
}
