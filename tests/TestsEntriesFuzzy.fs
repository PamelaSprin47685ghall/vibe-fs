module Wanxiangshu.Tests.TestsEntriesFuzzy

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestsTestBody
open Wanxiangshu.Tests.FuzzyPathTests
open Wanxiangshu.Tests.FuzzyFormatTests

let fuzzyTestEntries () : (string * TestBody) list =
    [
    "FuzzyPathTests.normalizePathConstraint", Sync (sync normalizePathConstraintTests)
    "FuzzyPathTests.normalizeExcludes", Sync (sync normalizeExcludesTests)
    "FuzzyPathTests.buildQuery", Sync (sync buildQueryTests)
    "FuzzyPathTests.resolveFuzzySearchPath", Sync (sync resolveFuzzySearchPathTests)
    "FuzzyPathTests.resolveExternalPath", Sync (sync resolveExternalPathTests)
    "FuzzyPathTests.resolveExternalBasePathForTest", Sync (sync resolveExternalBasePathForTestTests)
    "FuzzyFormatTests.truncateLine", Sync (sync truncateLineTests)
    "FuzzyFormatTests.fileAnnotation", Sync (sync fileAnnotationTests)
    "FuzzyFormatTests.formatGrepOutput", Sync (sync formatGrepOutputTests)
    "FuzzyFormatTests.formatFindOutput", Sync (sync formatFindOutputTests)
    ]
