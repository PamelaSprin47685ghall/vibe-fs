module Wanxiangshu.Omp.OmpTestHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FuzzyPath
open Wanxiangshu.Kernel.FuzzyQuery
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime

// Test-only iterator namespace; runtime fuzzy_find/fuzzy_grep use per-session scope in FuzzyTools.scopeId.
let private fuzzyScope = "global"

let globalIteratorStore = createTypedIteratorStore 200

let resetFuzzyState () : unit = clearTypedIteratorStore globalIteratorStore

let private mapFindStateFromJs (state: obj) : FuzzyFindState =
    let ext =
        match opt state "externalBasePath" with
        | Some v -> Some(string v)
        | None -> None
    { query = str state "query"
      pageSize = getValue<int> state "pageSize"
      pageIndex = getValue<int> state "pageIndex"
      externalBasePath = ext }

let private mapGrepStateFromJs (state: obj) : GrepIteratorState =
    let extPath = str state "externalBasePath"
    let ext = if extPath = "" then None else Some extPath
    let cursor =
        let c = get state "cursor"
        if isNullish c then None else Some c
    let core =
        { query = str state "query"
          mode = "plain"
          smartCase = true
          beforeContext = 0
          afterContext = 0
          pageSize = 50
          externalBasePath = ext }
    { core = core; cursor = cursor }

let private mapGrepStateToJs (state: GrepIteratorState) : obj =
    createObj [
        "externalBasePath",
            match state.core.externalBasePath with
            | Some p -> box p
            | None -> null
        "query", box state.core.query
        "cursor",
            match state.cursor with
            | Some c -> c
            | None -> null
    ]

let private mapFindStateToJs (state: FuzzyFindState) : obj =
    createObj [
        "query", box state.query
        "pageSize", box state.pageSize
        "pageIndex", box state.pageIndex
    ]

let resolveExternalBasePath (absPath: string) : obj =
    let r = resolveExternalBasePathForTest absPath
    createObj [
        "basePath", box r.basePath
        "pathConstraint",
            match r.pathConstraint with
            | Some s -> box s
            | None -> null
    ]

let createFuzzyTestExports () : obj =
    createObj [
        "resetFuzzyState", box resetFuzzyState
        "resolveExternalBasePath", box resolveExternalBasePath
        "storeCursor",
            box(fun (state: obj) ->
                storeGrepIterator globalIteratorStore fuzzyScope (mapGrepStateFromJs state))
        "consumeCursor",
            box(fun (id: string) ->
                match consumeGrepIterator globalIteratorStore id with
                | Some s -> mapGrepStateToJs s
                | None -> undefinedValue)
        "storeFindCursor",
            box(fun (state: obj) ->
                storeFindIterator globalIteratorStore fuzzyScope (mapFindStateFromJs state))
        "consumeFindCursor",
            box(fun (id: string) ->
                match consumeFindIterator globalIteratorStore id with
                | Some s -> mapFindStateToJs s
                | None -> undefinedValue)
    ]

let resetReviewStates (store: ReviewStore) : unit = store.clearReviewSessions ()

let setPendingReviewStateForTest (store: ReviewStore) (sessionId: string) (parentId: string) (pending: obj) : unit =
    store.addChild(parentId, sessionId)
    store.setPendingReview(sessionId, fun kr ->
        let js =
            match kr with
            | Accepted ->
                createObj [ "accepted", box true; "feedback", null; "terminated", null ]
            | Rejected fb ->
                createObj [ "accepted", box false; "feedback", box fb; "terminated", null ]
            | Terminated ->
                createObj [
                    "accepted", box false
                    "feedback", box "Review session closed."
                    "terminated", box true
                ]
        emitJsExpr (pending, js)
            """((p, r) => {
                if (typeof p === 'function') p(r);
                else if (p && typeof p.resolve === 'function') p.resolve(r);
            })($0, $1)"""
        |> ignore)