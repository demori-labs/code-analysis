; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DL1001 | Design | Warning | RecordsShouldNotHaveMutablePropertiesAnalyzer
DL1002 | Design | Warning | RecordsShouldNotHaveMutablePropertyTypesAnalyzer
DL1003 | Design | Warning | RecordPrimaryConstructorTooManyParametersAnalyzer
DL1004 | Design | Info | DataClassCouldBeRecordAnalyzer
DL1005 | Design | Warning | UsePrimaryConstructorAnalyzer
DL2001 | Usage | Error | ReadOnlyParameterAnalyzer
DL2002 | Usage | Error | ReadOnlyParameterAnalyzer
DL2003 | Usage | Warning | SuggestReadOnlyPrimaryConstructorParameterAnalyzer
DL2004 | Usage | Info | SuggestReadOnlyMethodParameterAnalyzer
DL3001 | Style | Warning | NamedArgumentAnalyzer
DL3002 | Style | Warning | InvertIfToReduceNestingAnalyzer
DL3003 | Style | Warning | ConstantPatternAnalyzer
DL3004 | Style | Warning | NegationPatternAnalyzer
DL3005 | Style | Warning | LogicalPatternAnalyzer
DL3006 | Style | Warning | TypeCheckAndCastAnalyzer
DL3007 | Style | Warning | AsWithNullCheckAnalyzer
DL4001 | Complexity | Info | CognitiveComplexityAnalyzer
DL4002 | Complexity | Warning | CognitiveComplexityAnalyzer
DL3008 | Style | Warning | BooleanReturnAnalyzer
DL3009 | Style | Warning | BooleanAssignmentAnalyzer
DL3010 | Style | Info | ConditionalReturnAnalyzer
DL3011 | Style | Warning | ConditionalAssignmentAnalyzer
DL3012 | Style | Warning | MergeNestedIfAnalyzer
DL3013 | Style | Warning | NullCoalescingAnalyzer
DL3014 | Style | Warning | NullCoalescingAssignmentAnalyzer
DL3015 | Style | Warning | NullConditionalAssignmentAnalyzer
DL5001 | Performance | Warning | MultipleEnumerationAnalyzer
DL3016 | Style | Warning | RedundantTypePatternAnalyzer
DL2005 | Usage | Warning | UnusedParameterAnalyzer
DL3017 | Style | Warning | UseStringEqualsAnalyzer
DL5002 | Performance | Warning | UseIsNullOrEmptyAnalyzer
