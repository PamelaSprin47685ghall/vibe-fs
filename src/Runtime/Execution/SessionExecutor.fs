module Wanxiangshu.Runtime.SessionExecutor

open Fable.Core
open Wanxiangshu.Runtime.RuntimeScope

let private activeRunsKey = "wanxiangshu.session_executor.active_runs"

let private getActiveRuns (scope: RuntimeScope) : Map<string, Map<string, unit -> unit>> =
    match scope.TryFindKey activeRunsKey with
    | Some v -> unbox<Map<string, Map<string, unit -> unit>>> v
    | None -> Map.empty

let private setActiveRuns (scope: RuntimeScope) (runs: Map<string, Map<string, unit -> unit>>) : unit =
    scope.Add(activeRunsKey, box runs)

let registerActiveRun (scope: RuntimeScope) (sessionId: string) (kill: unit -> unit) : unit -> unit =
    if sessionId = "" then
        fun () -> ()
    else
        let runId = System.Guid.NewGuid().ToString("N")
        let activeRuns = getActiveRuns scope
        let currentMap = Map.tryFind sessionId activeRuns |> Option.defaultValue Map.empty
        let updatedMap = Map.add runId kill currentMap
        setActiveRuns scope (Map.add sessionId updatedMap activeRuns)

        fun () ->
            let activeRuns = getActiveRuns scope

            match Map.tryFind sessionId activeRuns with
            | None -> ()
            | Some currentMap ->
                let updatedMap = Map.remove runId currentMap

                if Map.isEmpty updatedMap then
                    setActiveRuns scope (Map.remove sessionId activeRuns)
                else
                    setActiveRuns scope (Map.add sessionId updatedMap activeRuns)

let unregisterActiveRun (scope: RuntimeScope) (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then
        let activeRuns = getActiveRuns scope

        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some currentMap ->
            match
                currentMap
                |> Map.toSeq
                |> Seq.tryFind (fun (_, k) -> System.Object.ReferenceEquals(k, kill))
            with
            | Some(runId, _) ->
                let updatedMap = Map.remove runId currentMap

                if Map.isEmpty updatedMap then
                    setActiveRuns scope (Map.remove sessionId activeRuns)
                else
                    setActiveRuns scope (Map.add sessionId updatedMap activeRuns)
            | None -> ()

let hasActiveExecutorRun (scope: RuntimeScope) (sessionId: string) : bool =
    if sessionId = "" then
        false
    else
        match Map.tryFind sessionId (getActiveRuns scope) with
        | Some m -> not (Map.isEmpty m)
        | None -> false

let abortExecutorRun (scope: RuntimeScope) (sessionId: string) : unit =
    if sessionId <> "" then
        let activeRuns = getActiveRuns scope

        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some currentMap ->
            for KeyValue(_, kill) in currentMap do
                try
                    kill ()
                with _ ->
                    ()

            setActiveRuns scope (Map.remove sessionId activeRuns)

let resetSessionExecutorForTesting (scope: RuntimeScope) : unit = scope.Remove(activeRunsKey)

/// Per-session serial executor bound to a registration [RuntimeScope].
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work)

    member _.EnqueueExecutor(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueueExecutor(sessionId, work)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)
