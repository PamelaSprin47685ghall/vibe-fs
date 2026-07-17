module Wanxiangshu.Runtime.FuzzySearch

// Public entry re-exporting FuzzySearchSupport/Find/Grep.

open Wanxiangshu.Runtime.FuzzySearchSupport
open Wanxiangshu.Runtime.FuzzySearchFind
open Wanxiangshu.Runtime.FuzzySearchGrep
open Fable.Core
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Runtime.FuzzyIteratorStore

type SearchOptions = FuzzySearchSupport.SearchOptions
type ResolvedGrep = FuzzySearchSupport.ResolvedGrep
type FuzzyContinueParams = Wanxiangshu.Kernel.FuzzyQuery.FuzzyContinueParams

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

let parseExcludeField = FuzzySearchSupport.parseExcludeField
let optStr = FuzzySearchSupport.optStr
let optInt = FuzzySearchSupport.optInt
let optBool = FuzzySearchSupport.optBool
let resolveStore = FuzzySearchSupport.resolveStore
let runWithFinder = FuzzySearchSupport.runWithFinder

// Re-export state resolvers used by tests
let resolveFindSearchState = FuzzySearchFind.resolveFindSearchState
let resolveGrepIteratorState = FuzzySearchGrep.resolveGrepIteratorState
let findNextIterator = FuzzySearchFind.findNextIterator

// Re-export result resolver used by tests
let resolveResult = FuzzySearchGrep.resolveResult
