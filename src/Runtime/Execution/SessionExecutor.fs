module Wanxiangshu.Runtime.SessionExecutor

open Fable.Core
open Wanxiangshu.Runtime.RuntimeScope

let private activeRunsKey = "wanxiangshu.session_executor.active_runs"

let private getActiveRuns (scope: RuntimeScope) : Map<string, (unit -> unit) list> =
    match scope.TryFindKey activeRunsKey with
    | Some v -> unbox<Map<string, (unit -> unit) list>> v
    | None -> Map.empty

let private setActiveRuns (scope: RuntimeScope) (runs: Map<string, (unit -> unit) list>) : unit =
    scope.Add(activeRunsKey, box runs)

let registerActiveRun (scope: RuntimeScope) (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then
        let activeRuns = getActiveRuns scope
        let current = Map.tryFind sessionId activeRuns |> Option.defaultValue []
        setActiveRuns scope (Map.add sessionId (kill :: current) activeRuns)

let unregisterActiveRun (scope: RuntimeScope) (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then
        let activeRuns = getActiveRuns scope

        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some current ->
            let updated =
                current |> List.filter (fun k -> not (System.Object.ReferenceEquals(k, kill)))

            if List.isEmpty updated then
                setActiveRuns scope (Map.remove sessionId activeRuns)
            else
                setActiveRuns scope (Map.add sessionId updated activeRuns)

let hasActiveExecutorRun (scope: RuntimeScope) (sessionId: string) : bool =
    sessionId <> "" && Map.containsKey sessionId (getActiveRuns scope)

let abortExecutorRun (scope: RuntimeScope) (sessionId: string) : unit =
    if sessionId <> "" then
        let activeRuns = getActiveRuns scope

        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some kills ->
            for kill in kills do
                try
                    kill ()
                with _ ->
                    ()

            setActiveRuns scope (Map.remove sessionId activeRuns)

let resetSessionExecutorForTesting (scope: RuntimeScope) : unit = scope.Remove(activeRunsKey)

/// Per-session serial executor bound to a registration [RuntimeScope].
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>, ?timeoutMs: int) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work, ?timeoutMs = timeoutMs)

    member _.EnqueueExecutor(sessionId: string, mode: string, work: unit -> JS.Promise<'T>, ?timeoutMs: int) : JS.Promise<'T> =
        scope.EnqueueExecutor(sessionId, mode, work, ?timeoutMs = timeoutMs)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)
