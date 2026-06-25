module VibeFs.Shell.SessionExecutor

open Fable.Core
open VibeFs.Shell.RuntimeScope

let mutable private activeRuns : Map<string, unit -> unit> = Map.empty

let registerActiveRun (sessionId: string) (kill: unit -> unit) : unit =
    if sessionId <> "" then activeRuns <- Map.add sessionId kill activeRuns

let unregisterActiveRun (sessionId: string) : unit =
    if sessionId <> "" then activeRuns <- Map.remove sessionId activeRuns

let hasActiveExecutorRun (sessionId: string) : bool =
    sessionId <> "" && Map.containsKey sessionId activeRuns

let abortExecutorRun (sessionId: string) : unit =
    if sessionId = "" then ()
    else
        match Map.tryFind sessionId activeRuns with
        | None -> ()
        | Some kill ->
            try kill () with _ -> ()
            unregisterActiveRun sessionId

let resetSessionExecutorForTesting () : unit =
    activeRuns <- Map.empty

/// Per-session serial executor bound to a registration [RuntimeScope].
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)