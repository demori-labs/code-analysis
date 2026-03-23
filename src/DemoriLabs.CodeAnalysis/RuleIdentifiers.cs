namespace DemoriLabs.CodeAnalysis;

internal static class RuleIdentifiers
{
    // DL1xxx — Design
    public const string RecordsShouldNotHaveMutableProperties = "DL1001";
    public const string RecordsShouldNotHaveMutablePropertyTypes = "DL1002";
    public const string RecordPrimaryConstructorTooManyParameters = "DL1003";
    public const string DataClassCouldBeRecord = "DL1004";
    public const string UsePrimaryConstructor = "DL1005";

    // DL2xxx — Usage
    public const string ReadOnlyParameter = "DL2001";
    public const string ReadOnlyIncompatibleModifier = "DL2002";
    public const string SuggestReadOnlyPrimaryConstructorParameter = "DL2003";
    public const string MutableIncompatibleModifier = "DL2004";

    // DL3xxx — Style
    public const string NamedArgument = "DL3001";
    public const string InvertIfToReduceNesting = "DL3002";

    // DL4xxx — Complexity
    public const string MethodHasModerateCognitiveComplexity = "DL4001";
    public const string MethodHasElevatedCognitiveComplexity = "DL4002";

    // DL5xxx — Performance
    public const string PossibleMultipleEnumeration = "DL5001";
}
