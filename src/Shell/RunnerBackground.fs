module Wanxiangshu.Shell.RunnerBackground

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.SessionExecutor

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (content: string) : unit = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("existsSync", "node:fs")>]
let private existsSync (path: string) : bool = jsNative

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

type private RunnerJobEntry = { status: string }

type private RunnerState = {
    Jobs: Map<string, RunnerJobEntry>
    ActiveSessions: Set<string>
    LogBuffers: Map<string, string>
    ChildByParent: Map<string, string>
    ChildDispose: Map<string, unit -> unit>
}

let private emptyState = {
    Jobs = Map.empty
    ActiveSessions = Set.empty
    LogBuffers = Map.empty
    ChildByParent = Map.empty
    ChildDispose = Map.empty
}

let mutable private state = emptyState

let registerActiveRunnerSession (sessionId: string) : unit =
    if sessionId <> "" then state <- { state with ActiveSessions = Set.add sessionId state.ActiveSessions }

let unregisterActiveRunnerSession (sessionId: string) : unit =
    if sessionId <> "" then state <- { state with ActiveSessions = Set.remove sessionId state.ActiveSessions }

let registerRunnerChild (parentSessionId: string) (childSessionId: string) (dispose: unit -> unit) : unit =
    if parentSessionId <> "" && childSessionId <> "" then
        state <- { state with
                    ChildByParent = Map.add parentSessionId childSessionId state.ChildByParent
                    ChildDispose = Map.add parentSessionId dispose state.ChildDispose }

let unregisterRunnerChild (parentSessionId: string) : unit =
    if parentSessionId <> "" then
        state <- { state with
                    ChildByParent = Map.remove parentSessionId state.ChildByParent
                    ChildDispose = Map.remove parentSessionId state.ChildDispose }

let appendRunnerLog (sessionId: string) (chunk: string) : unit =
    if sessionId <> "" && chunk <> "" then
        let prev = Map.tryFind sessionId state.LogBuffers |> Option.defaultValue ""
        state <- { state with LogBuffers = Map.add sessionId (prev + chunk) state.LogBuffers }

let private childActiveForParent (parentSessionId: string) : bool =
    match Map.tryFind parentSessionId state.ChildByParent with
    | None -> false
    | Some childId -> hasActiveExecutorRun childId

let hasRunningRunnerJob (sessionId: string) : bool =
    hasActiveExecutorRun sessionId
    || Set.contains sessionId state.ActiveSessions
    || Map.containsKey sessionId state.Jobs
    || childActiveForParent sessionId

let getRunnerLogPathForTest (sessionId: string) : string =
    pathJoin (tmpdir ()) $"omp-runner-test-{sessionId}.log"

let private readLogSnippet (sessionId: string) : string =
    match Map.tryFind sessionId state.LogBuffers with
    | Some buf when buf <> "" -> buf
    | _ ->
        let path = getRunnerLogPathForTest sessionId
        if existsSync path then readFileSync path "utf-8" else ""

let private tailLines (text: string) (maxChars: int) : string =
    if text.Length <= maxChars then text
    else text.Substring(text.Length - maxChars)

let waitRunnerJob (sessionId: string) (ms: int) : JS.Promise<string> =
    promise {
        do! Promise.sleep ms
        let snippet = tailLines (readLogSnippet sessionId) 8000
        return if snippet = "" then "(no new output)" else snippet
    }

let private abortRunnerJobCore (sessionId: string) : unit =
    let childId = Map.tryFind sessionId state.ChildByParent
    childId |> Option.iter abortExecutorRun
    abortExecutorRun sessionId
    match Map.tryFind sessionId state.ChildDispose with
    | None -> ()
    | Some dispose ->
        try dispose () with _ -> ()
    unregisterRunnerChild sessionId
    unregisterActiveRunnerSession sessionId
    state <- { state with Jobs = Map.remove sessionId state.Jobs; LogBuffers = Map.remove sessionId state.LogBuffers }

let abortRunnerJob (sessionId: string) : string =
    abortRunnerJobCore sessionId
    "Runner abort requested."

let cleanupRunnerJob (sessionId: string) : JS.Promise<unit> =
    promise { abortRunnerJobCore sessionId }

let resetRunnerJobsForTesting () : unit =
    state <- emptyState
    resetSessionExecutorForTesting ()

let setRunnerJobStateForTest (sessionId: string) (status: string) : unit =
    writeFileSync (getRunnerLogPathForTest sessionId) ""
    state <- { state with Jobs = Map.add sessionId { status = status } state.Jobs }
