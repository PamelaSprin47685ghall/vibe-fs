module Wanxiangshu.Shell.FuzzySearch

// Thin facade re-exporting the public API from FuzzySearchHelpers/Find/Grep
// for backward compatibility. All consumers use module Wanxiangshu.Shell.FuzzySearch.

open Wanxiangshu.Shell.FuzzySearchHelpers
open Wanxiangshu.Shell.FuzzySearchFind
open Wanxiangshu.Shell.FuzzySearchGrep

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
