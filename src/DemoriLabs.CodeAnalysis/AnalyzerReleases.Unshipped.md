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
DL3001 | Style | Warning | NamedArgumentAnalyzer
DL3002 | Style | Warning | InvertIfToReduceNestingAnalyzer
DL4001 | Complexity | Info | CognitiveComplexityAnalyzer
DL4002 | Complexity | Warning | CognitiveComplexityAnalyzer
DL5001 | Performance | Warning | MultipleEnumerationAnalyzer
