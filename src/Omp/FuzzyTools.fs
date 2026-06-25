module VibeFs.Omp.FuzzyTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Omp.Codec
open VibeFs.Omp.Schema
module Dyn = VibeFs.Shell.Dyn
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.FuzzySearch
open VibeFs.Kernel.FuzzyQuery

let private scopeId (ctx: obj) =
    let sid = Dyn.str ctx "sessionId"
    if sid <> "" then sid else Dyn.str ctx "workspaceId"

let registerFuzzyTools (pi: obj) (finderCache: FinderCache) : unit =
    let tb = Dyn.get pi "typebox"
    pi?registerTool(
        createObj [
            "name", box "fuzzy_find"
            "label", box "Fuzzy Find"
            "description", box fuzzyFindDescriptionOmp
            "parameters",
                objectOf
                    [|
                        ("pattern", opt "Initial plain fuzzy file path text to search for." tb str)
                        ("path", opt "Initial optional path constraint to narrow search scope" tb str)
                        ("limit", opt "Maximum number of results to return per call (default: 30)" tb num)
                        ("iterator", opt "Opaque single-use iterator from a previous fuzzy_find result." tb str)
                    |]
                    tb
            "execute",
                box(
                    fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                        promise {
                            let scope = scopeId ctx
                            if scope = "" then
                                return errorResult "fuzzy_find requires an active session"
                            else
                                let p =
                                    { pattern = optStr params' "pattern"
                                      path = optStr params' "path"
                                      limit = optInt params' "limit"
                                      iterator = optStr params' "iterator" }
                                let opts =
                                    { cwd = Dyn.str ctx "cwd"
                                      scopeId = scope
                                      store = None
                                      finderCache = finderCache }
                                let! r = fuzzyFind p opts
                                if r.isError then return errorResult r.output else return textResult r.output
                        })
        ])

    pi?registerTool(
        createObj [
            "name", box "fuzzy_grep"
            "label", box "Fuzzy Grep"
            "description", box fuzzyGrepDescriptionOmp
            "parameters",
                objectOf
                    [|
                        ("pattern", opt "Initial search pattern. Required on the first call." tb str)
                        ("path", opt "Initial path constraint." tb str)
                        ("exclude",
                            optional(
                                union
                                    [| str "Initial exclude paths (e.g. 'test/,*.min.js')" tb
                                       strArray "Initial exclude path or glob" tb |]
                                    tb)
                                tb)
                        ("caseSensitive", opt "Case-sensitivity override (smart-case by default)." tb bool_)
                        ("context", opt "Number of context lines before and after each match" tb num)
                        ("limit", opt "Maximum number of matches to return per call." tb num)
                        ("iterator", opt "Opaque single-use iterator from a previous fuzzy_grep result." tb str)
                    |]
                    tb
            "execute",
                box(
                    fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                        promise {
                            let scope = scopeId ctx
                            if scope = "" then
                                return errorResult "fuzzy_grep requires an active session"
                            else
                                let exclude = parseExcludeField params'
                                let p =
                                    { pattern = optStr params' "pattern"
                                      path = optStr params' "path"
                                      exclude = exclude
                                      caseSensitive = optBool params' "caseSensitive"
                                      context = optInt params' "context"
                                      limit = optInt params' "limit"
                                      iterator = optStr params' "iterator" }
                                let opts =
                                    { cwd = Dyn.str ctx "cwd"
                                      scopeId = scope
                                      store = None
                                      finderCache = finderCache }
                                let! r = fuzzyGrep p opts
                                if r.isError then return errorResult r.output else return textResult r.output
                        })
        ])