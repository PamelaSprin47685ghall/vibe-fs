module Wanxiangshu.Shell.FuzzySearch

// Thin facade re-exporting the public API from FuzzySearchHelpers/Find/Grep
// for backward compatibility. All consumers use module Wanxiangshu.Shell.FuzzySearch.

open Wanxiangshu.Shell.FuzzySearchHelpers
open Wanxiangshu.Shell.FuzzySearchFind
open Wanxiangshu.Shell.FuzzySearchGrep
open Fable.Core
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Shell.FuzzyIteratorStore

// Re-export types
type SearchOptions = FuzzySearchHelpers.SearchOptions
type ResolvedGrep = FuzzySearchHelpers.ResolvedGrep
type FuzzyContinueParams = Wanxiangshu.Kernel.FuzzyQuery.FuzzyContinueParams

// Re-export main entry points
let fuzzyFind = FuzzySearchFind.fuzzyFind
let fuzzyGrep = FuzzySearchGrep.fuzzyGrep

let fuzzyContinue (params': FuzzyContinueParams) (opts: SearchOptions) : JS.Promise<SearchOutcome> =
    promise {
        match resolveStore opts with
        | Error msg -> return { output = msg; isError = true }
        | Ok store ->
            let it = params'.iterator

            let isFind =
                it.Contains(":" + findIteratorNamespace + ":")
                || it.StartsWith(findIteratorNamespace)

            let isGrep =
                it.Contains(":" + grepIteratorNamespace + ":")
                || it.StartsWith(grepIteratorNamespace)

            if isFind then
                match consumeFindIterator store it with
                | Some state -> return! FuzzySearchFind.fuzzyFindContinue state store opts
                | None ->
                    return
                        { output = iteratorError "fuzzy_continue" it
                          isError = true }
            elif isGrep then
                match consumeGrepIterator store it with
                | Some state -> return! FuzzySearchGrep.fuzzyGrepContinue state store opts
                | None ->
                    return
                        { output = iteratorError "fuzzy_continue" it
                          isError = true }
            else
                return
                    { output = $"fuzzy_continue error: invalid iterator format \"{it}\""
                      isError = true }
    }

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
