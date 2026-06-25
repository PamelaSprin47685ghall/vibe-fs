module VibeFs.Shell.RunnerBackground

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Shell.SessionExecutor

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

let mutable private runnerJobs : Map<string, RunnerJobEntry> = Map.empty

let mutable private activeRunnerSessions : Set<string> = Set.empty

let mutable private logBuffers : Map<string, string> = Map.empty

let mutable private childByParent : Map<string, string> = Map.empty

let mutable private childDispose : Map<string, unit -> unit> = Map.empty

let registerActiveRunnerSession (sessionId: string) : unit =
    if sessionId <> "" then activeRunnerSessions <- Set.add sessionId activeRunnerSessions

let unregisterActiveRunnerSession (sessionId: string) : unit =
    if sessionId <> "" then activeRunnerSessions <- Set.remove sessionId activeRunnerSessions

let registerRunnerChild (parentSessionId: string) (childSessionId: string) (dispose: unit -> unit) : unit =
    if parentSessionId <> "" && childSessionId <> "" then
        childByParent <- Map.add parentSessionId childSessionId childByParent
        childDispose <- Map.add parentSessionId dispose childDispose

let unregisterRunnerChild (parentSessionId: string) : unit =
    if parentSessionId <> "" then
        childByParent <- Map.remove parentSessionId childByParent
        childDispose <- Map.remove parentSessionId childDispose

let appendRunnerLog (sessionId: string) (chunk: string) : unit =
    if sessionId <> "" && chunk <> "" then
        let prev = Map.tryFind sessionId logBuffers |> Option.defaultValue ""
        logBuffers <- Map.add sessionId (prev + chunk) logBuffers

let private childActiveForParent (parentSessionId: string) : bool =
    match Map.tryFind parentSessionId childByParent with
    | None -> false
    | Some childId -> hasActiveExecutorRun childId

let hasRunningRunnerJob (sessionId: string) : bool =
    hasActiveExecutorRun sessionId
    || Set.contains sessionId activeRunnerSessions
    || Map.containsKey sessionId runnerJobs
    || childActiveForParent sessionId

let getRunnerLogPathForTest (sessionId: string) : string =
    pathJoin (tmpdir ()) $"omp-runner-test-{sessionId}.log"

let private readLogSnippet (sessionId: string) : string =
    match Map.tryFind sessionId logBuffers with
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
    let childId = Map.tryFind sessionId childByParent
    childId |> Option.iter abortExecutorRun
    abortExecutorRun sessionId
    match Map.tryFind sessionId childDispose with
    | None -> ()
    | Some dispose ->
        try dispose () with _ -> ()
    unregisterRunnerChild sessionId
    unregisterActiveRunnerSession sessionId
    runnerJobs <- Map.remove sessionId runnerJobs
    logBuffers <- Map.remove sessionId logBuffers

let abortRunnerJob (sessionId: string) : string =
    abortRunnerJobCore sessionId
    "Runner abort requested."

let cleanupRunnerJob (sessionId: string) : JS.Promise<unit> =
    promise {
        abortRunnerJobCore sessionId
    }

let resetRunnerJobsForTesting () : unit =
    activeRunnerSessions <- Set.empty
    runnerJobs <- Map.empty
    logBuffers <- Map.empty
    childByParent <- Map.empty
    childDispose <- Map.empty
    resetSessionExecutorForTesting ()

let setRunnerJobStateForTest (sessionId: string) (status: string) : unit =
    writeFileSync (getRunnerLogPathForTest sessionId) ""
    runnerJobs <- Map.add sessionId { status = status } runnerJobs