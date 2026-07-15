module Wanxiangshu.Omp.FuzzyTools

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.Schema

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.FuzzySearch
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Kernel.FuzzyQuery

let private scopeId (ctx: obj) =
    let sid = Dyn.str ctx "sessionId"
    if sid <> "" then sid else Dyn.str ctx "workspaceId"

let registerFuzzyTools (pi: obj) (finderCache: FinderCache) (iteratorStore: TypedIteratorStore) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box "fuzzy_find"
              "label", box "Fuzzy Find"
              "description", box fuzzyFindDescriptionOmp
              "parameters",
              objectOf
                  [| ("pattern",
                      strArray
                          """Plain fuzzy file path text to search for. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Correct: ["src","build"]. Wrong: "[\"src\",\"build\"]" (a string, not an array)."""
                          tb)
                     ("path", opt "Initial optional path constraint to narrow search scope" tb str)
                     ("limit", opt "Maximum number of results to return per call (default: 30)" tb num) |]
                  tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  promise {
                      let scope = scopeId ctx

                      if scope = "" then
                          return errorResult "fuzzy_find requires an active session"
                      else
                          match Wanxiangshu.Shell.FuzzyToolsCodec.decodeFuzzyFindArgs params' with
                          | Error e -> return errorResult (string e)
                          | Ok p ->
                              let opts: SearchOptions =
                                  { cwd = Dyn.str ctx "cwd"
                                    scopeId = scope
                                    store = Some iteratorStore
                                    finderCache = finderCache }

                              let! r = fuzzyFind p opts

                              if r.isError then
                                  return errorResult r.output
                              else
                                  return textResult r.output
                  }) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "fuzzy_grep"
              "label", box "Fuzzy Grep"
              "description", box fuzzyGrepDescriptionOmp
              "parameters",
              objectOf
                  [| ("pattern",
                      strArray
                          """Search pattern. Pass a real JSON array of strings for parallel search; never pass a stringified JSON string. Required on the first call. Correct: ["StateMachine","EventLog"]. Wrong: "[\"StateMachine\",\"EventLog\"]" (a string, not an array)."""
                          tb)
                     ("path", opt "Initial path constraint." tb str)
                     ("exclude",
                      optional
                          (union
                              [| str "Initial exclude paths (e.g. 'test/,*.min.js')" tb
                                 strArray "Initial exclude path or glob" tb |]
                              tb)
                          tb)
                     ("caseSensitive", opt "Case-sensitivity override (smart-case by default)." tb bool_)
                     ("searchIgnored",
                      opt
                          "Search git-ignored files such as node_modules by adding the fff git:ignored constraint."
                          tb
                          bool_)
                     ("context", opt "Number of context lines before and after each match" tb num)
                     ("limit", opt "Maximum number of matches to return per call." tb num) |]
                  tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  promise {
                      let scope = scopeId ctx

                      if scope = "" then
                          return errorResult "fuzzy_grep requires an active session"
                      else
                          match Wanxiangshu.Shell.FuzzyToolsCodec.decodeFuzzyGrepArgs params' with
                          | Error e -> return errorResult (string e)
                          | Ok p ->
                              let opts: SearchOptions =
                                  { cwd = Dyn.str ctx "cwd"
                                    scopeId = scope
                                    store = Some iteratorStore
                                    finderCache = finderCache }

                              let! r = fuzzyGrep p opts

                              if r.isError then
                                  return errorResult r.output
                              else
                                  return textResult r.output
                  }) ]
    )

    pi?registerTool (
        createObj
            [ "name", box "fuzzy_continue"
              "label", box "Fuzzy Continue"
              "description",
              box "Continue a previously running fuzzy_find or fuzzy_grep session. Returns the next page of results."
              "parameters",
              objectOf [| ("iterator", str "Opaque single-use iterator from a previous search result." tb) |] tb
              "execute",
              box (fun (_id: string) (params': obj) (_signal: obj) (_onUpdate: obj) (ctx: obj) ->
                  promise {
                      let scope = scopeId ctx

                      if scope = "" then
                          return errorResult "fuzzy_continue requires an active session"
                      else
                          match Wanxiangshu.Shell.FuzzyToolsCodec.decodeFuzzyContinueArgs params' with
                          | Error e -> return errorResult (string e)
                          | Ok p ->
                              let opts: SearchOptions =
                                  { cwd = Dyn.str ctx "cwd"
                                    scopeId = scope
                                    store = Some iteratorStore
                                    finderCache = finderCache }

                              let! r = fuzzyContinue p opts

                              if r.isError then
                                  return errorResult r.output
                              else
                                  return textResult r.output
                  }) ]
    )
