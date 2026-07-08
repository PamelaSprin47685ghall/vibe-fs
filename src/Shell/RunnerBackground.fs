module Wanxiangshu.Shell.RunnerBackground

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.SessionExecutor

type private RunnerJobEntry = { status: string }

type private RunnerState =
    { Jobs: Map<string, RunnerJobEntry>
      ActiveSessions: Set<string>
      LogBuffers: Map<string, string>
      ChildByParent: Map<string, string>
      ChildDispose: Map<string, unit -> unit> }

let private emptyState =
    { Jobs = Map.empty
      ActiveSessions = Set.empty
      LogBuffers = Map.empty
      ChildByParent = Map.empty
      ChildDispose = Map.empty }

let private runnerStateKey = "wanxiangshu.runner_state"

let private getState (scope: RuntimeScope) : RunnerState =
    match scope.TryFindKey runnerStateKey with
    | Some v -> unbox<RunnerState> v
    | None -> emptyState

let private setState (scope: RuntimeScope) (s: RunnerState) = scope.Add(runnerStateKey, box s)

let private updateState scope f = setState scope (f (getState scope))

let registerActiveRunnerSession (scope: RuntimeScope) (sessionId: string) : unit =
    if sessionId <> "" then
        updateState scope (fun s ->
            { s with
                ActiveSessions = Set.add sessionId s.ActiveSessions })

let unregisterActiveRunnerSession (scope: RuntimeScope) (sessionId: string) : unit =
    if sessionId <> "" then
        updateState scope (fun s ->
            { s with
                ActiveSessions = Set.remove sessionId s.ActiveSessions })

let registerRunnerChild
    (scope: RuntimeScope)
    (parentSessionId: string)
    (childSessionId: string)
    (dispose: unit -> unit)
    : unit =
    if parentSessionId <> "" && childSessionId <> "" then
        updateState scope (fun s ->
            { s with
                ChildByParent = Map.add parentSessionId childSessionId s.ChildByParent
                ChildDispose = Map.add parentSessionId dispose s.ChildDispose })

let unregisterRunnerChild (scope: RuntimeScope) (parentSessionId: string) : unit =
    if parentSessionId <> "" then
        updateState scope (fun s ->
            { s with
                ChildByParent = Map.remove parentSessionId s.ChildByParent
                ChildDispose = Map.remove parentSessionId s.ChildDispose })

let appendRunnerLog (scope: RuntimeScope) (sessionId: string) (chunk: string) : unit =
    if sessionId <> "" && chunk <> "" then
        let prev =
            Map.tryFind sessionId (getState scope).LogBuffers |> Option.defaultValue ""

        updateState scope (fun s ->
            { s with
                LogBuffers = Map.add sessionId (prev + chunk) s.LogBuffers })

let private childActiveForParent (scope: RuntimeScope) (parentSessionId: string) : bool =
    match Map.tryFind parentSessionId (getState scope).ChildByParent with
    | None -> false
    | Some childId -> hasActiveExecutorRun childId

let hasRunningRunnerJob (scope: RuntimeScope) (sessionId: string) : bool =
    hasActiveExecutorRun sessionId
    || Set.contains sessionId (getState scope).ActiveSessions
    || Map.containsKey sessionId (getState scope).Jobs
    || childActiveForParent scope sessionId

let private readLogSnippet (scope: RuntimeScope) (sessionId: string) : string =
    match Map.tryFind sessionId (getState scope).LogBuffers with
    | Some buf when buf <> "" -> buf
    | _ -> ""

let private tailLines (text: string) (maxChars: int) : string =
    if text.Length <= maxChars then
        text
    else
        text.Substring(text.Length - maxChars)

let waitRunnerJob (scope: RuntimeScope) (sessionId: string) (ms: int) : JS.Promise<string> =
    promise {
        do! Promise.sleep ms
        let snippet = tailLines (readLogSnippet scope sessionId) 8000
        return if snippet = "" then "(no new output)" else snippet
    }

let abortRunnerJobCore (scope: RuntimeScope) (sessionId: string) : unit =
    let childId = Map.tryFind sessionId (getState scope).ChildByParent
    childId |> Option.iter abortExecutorRun
    abortExecutorRun sessionId

    match Map.tryFind sessionId (getState scope).ChildDispose with
    | None -> ()
    | Some dispose ->
        try
            dispose ()
        with _ ->
            ()

    unregisterRunnerChild scope sessionId
    unregisterActiveRunnerSession scope sessionId

    updateState scope (fun s ->
        { s with
            Jobs = Map.remove sessionId s.Jobs
            LogBuffers = Map.remove sessionId s.LogBuffers })

let abortRunnerJob (scope: RuntimeScope) (sessionId: string) : string =
    abortRunnerJobCore scope sessionId
    "Runner abort requested."

let cleanupRunnerJob (scope: RuntimeScope) (sessionId: string) : JS.Promise<unit> =
    promise { abortRunnerJobCore scope sessionId }

let clearRunnerLogsForTest (scope: RuntimeScope) : unit =
    setState scope emptyState
    resetSessionExecutorForTesting ()
