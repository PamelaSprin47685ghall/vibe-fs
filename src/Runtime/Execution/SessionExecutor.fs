module Wanxiangshu.Runtime.SessionExecutor

open Fable.Core
open Wanxiangshu.Runtime.RuntimeScope

let mutable private activeRuns: Map<string, (unit -> unit) list> = Map.empty

let registerActiveRun (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then
        let current = Map.tryFind sessionId activeRuns |> Option.defaultValue []
        activeRuns <- Map.add sessionId (kill :: current) activeRuns

let unregisterActiveRun (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then
        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some current ->
            let updated =
                current |> List.filter (fun k -> not (System.Object.ReferenceEquals(k, kill)))

            if List.isEmpty updated then
                activeRuns <- Map.remove sessionId activeRuns
            else
                activeRuns <- Map.add sessionId updated activeRuns

let hasActiveExecutorRun (sessionId: string) : bool =
    sessionId <> "" && Map.containsKey sessionId activeRuns

let abortExecutorRun (sessionId: string) : unit =
    if sessionId <> "" then
        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some kills ->
            for kill in kills do
                try
                    kill ()
                with _ ->
                    ()

            activeRuns <- Map.remove sessionId activeRuns

let resetSessionExecutorForTesting () : unit = activeRuns <- Map.empty

/// Per-session serial executor bound to a registration [RuntimeScope].
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work)

    member _.EnqueueExecutor(sessionId: string, mode: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueueExecutor(sessionId, mode, work)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)
