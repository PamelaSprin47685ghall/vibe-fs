module VibeFs.Shell.FuzzySearch

// Thin facade re-exporting the public API from FuzzySearchHelpers/Find/Grep
// for backward compatibility. All consumers use module VibeFs.Shell.FuzzySearch.

open VibeFs.Shell.FuzzySearchHelpers
open VibeFs.Shell.FuzzySearchFind
open VibeFs.Shell.FuzzySearchGrep

// Re-export types
type SearchOptions = FuzzySearchHelpers.SearchOptions
type ResolvedGrep = FuzzySearchHelpers.ResolvedGrep

// Re-export main entry points
let fuzzyFind = FuzzySearchFind.fuzzyFind
let fuzzyGrep = FuzzySearchGrep.fuzzyGrep

// Re-export helpers used by FuzzyToolsCodec and tests
let parseExcludeField = FuzzySearchHelpers.parseExcludeField
let optStr = FuzzySearchHelpers.optStr
let optInt = FuzzySearchHelpers.optInt
let optBool = FuzzySearchHelpers.optBool
let resolveStore = FuzzySearchHelpers.resolveStore
let runWithFinder = FuzzySearchHelpers.runWithFinder

// Re-export state resolvers used by tests
let resolveFindSearchState = FuzzySearchFind.resolveFindSearchState
let resolveGrepIteratorState = FuzzySearchGrep.resolveGrepIteratorState
let findNextIterator = FuzzySearchFind.findNextIterator

// Re-export result resolver used by tests
let resolveResult = FuzzySearchGrep.resolveResult
