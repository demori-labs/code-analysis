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
    public const string IncompatibleAttributeModifier = "DL2002";
    public const string SuggestReadOnlyPrimaryConstructorParameter = "DL2003";
    public const string SuggestReadOnlyMethodParameter = "DL2004";
    public const string UnusedParameter = "DL2005";

    // DL3xxx — Style
    public const string NamedArgument = "DL3001";
    public const string InvertIfToReduceNesting = "DL3002";
    public const string UseConstantPattern = "DL3003";
    public const string UseNegationPattern = "DL3004";
    public const string UseLogicalPattern = "DL3005";
    public const string UseDeclarationPatternInsteadOfCast = "DL3006";
    public const string UseDeclarationPatternInsteadOfAs = "DL3007";
    public const string SimplifyBooleanReturn = "DL3008";
    public const string SimplifyBooleanAssignment = "DL3009";
    public const string SimplifyConditionalReturn = "DL3010";
    public const string SimplifyConditionalAssignment = "DL3011";
    public const string MergeNestedIf = "DL3012";
    public const string UseNullCoalescing = "DL3013";
    public const string UseNullCoalescingAssignment = "DL3014";
    public const string UseNullConditionalAssignment = "DL3015";
    public const string RedundantTypePattern = "DL3016";
    public const string UseStringEqualsWithComparison = "DL3017";

    // DL4xxx — Complexity
    public const string MethodHasModerateCognitiveComplexity = "DL4001";
    public const string MethodHasElevatedCognitiveComplexity = "DL4002";

    // DL5xxx — Performance
    public const string PossibleMultipleEnumeration = "DL5001";
    public const string UseIsNullOrEmpty = "DL5002";
}
