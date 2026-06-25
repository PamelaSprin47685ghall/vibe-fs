module VibeFs.Tests.KnowledgeGraphBookkeeperLaunchTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.DelegatedAiSettings
open VibeFs.Shell.Dyn
open VibeFs.Shell.KnowledgeGraphBookkeeperLaunch

let sessionApiOfNullClient () =
    check "sessionApiOf null" (sessionApiOf (box null) |> Option.isNone)

let sessionApiOfClientWithoutSession () =
    let client = createObj [ "foo", box 1 ]
    check "no session field" (sessionApiOf client |> Option.isNone)

let sessionApiOfSessionWithoutCreate () =
    let client = createObj [ "session", createObj [ "prompt", box (fun (_: obj) -> Promise.lift null) ] ]
    check "no create" (sessionApiOf client |> Option.isNone)

let queueBackgroundLaunchWithoutApiInvokesRecordResult () = promise {
    let mutable recorded = ""
    let registry = ChildAgentRegistry.Create()
    let started = ResizeArray<JS.Promise<unit>>()
    queueBackgroundLaunch
        (box null)
        (fun job ->
            started.Add job
            Promise.start job)
        (fun s -> recorded <- s)
        "root"
        None
        (DailyRewrite "2024-01-01")
        "Daily title"
        (fun () -> Promise.lift "prompt")
        emptySettings
        registry
    for i = 0 to started.Count - 1 do
        do! started.[i]
    check "recorded failure" (recorded.Contains "Daily title")
    check "missing apis" (recorded.Contains "session.create")
}

let private drainBackgroundJobs (started: ResizeArray<JS.Promise<unit>>) = promise {
    for i = 0 to started.Count - 1 do
        do! started.[i]
}

let queueMuxBackgroundLaunchSuccess () = promise {
    let mutable recorded = ""
    let started = ResizeArray<JS.Promise<unit>>()
    let okResult = "Started Mux daily in background session mux-child-ok."
    queueMuxBackgroundLaunch
        (box null)
        (box null)
        "bookkeeper"
        "Mux daily"
        None
        (fun job ->
            started.Add job
            Promise.start job)
        (fun s -> recorded <- s)
        (fun () -> Promise.lift "built prompt")
        (fun _ _ _ _ _ _ -> Promise.lift okResult)
    do! drainBackgroundJobs started
    equal "mux launch records delegate ok" okResult recorded
}

let queueMuxBackgroundLaunchDelegateThrows () = promise {
    let mutable recorded = ""
    let started = ResizeArray<JS.Promise<unit>>()
    let title = "Mux fail title"
    queueMuxBackgroundLaunch
        (box null)
        (box null)
        "bookkeeper"
        title
        None
        (fun job ->
            started.Add job
            Promise.start job)
        (fun s -> recorded <- s)
        (fun () -> Promise.lift "built prompt")
        (fun _ _ _ _ _ _ -> failwith "delegate exploded")
    do! drainBackgroundJobs started
    check "mux delegate throw mentions title" (recorded.Contains title)
    check "mux delegate throw failed to start" (recorded.Contains "Failed to start")
}

let launchBackgroundSessionHappyPath () = promise {
    let createCalls = ResizeArray<obj>()
    let promptCalls = ResizeArray<obj>()
    let childId = "child-bookkeeper-1"
    let session =
        createObj [
            "create", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    createCalls.Add arg
                    return box {| data = box {| id = childId |} |}
                }))
            "prompt", box (System.Func<obj, JS.Promise<obj>>(fun arg ->
                promise {
                    promptCalls.Add arg
                    return box null
                }))
        ]
    let registry = ChildAgentRegistry.Create()
    let title = "Daily rewrite job"
    let! result =
        launchBackgroundSession
            session
            "/workspace/root"
            (Some "parent-session-1")
            (DailyRewrite "2024-06-01")
            title
            "Maintain the knowledge graph."
            emptySettings
            (box null)
            registry
    check "launch result text" (result = $"Started {title} in background session {childId}.")
    equal "registry bookkeeper agent" (Some "bookkeeper") (registry.LookupChildAgent childId)
    check "create uses workspace root" (str (get createCalls.[0] "query") "directory" = "/workspace/root")
    check "create parent id" (str (get createCalls.[0] "body") "parentID" = "parent-session-1")
    check "prompt targets child" (str (get promptCalls.[0] "path") "id" = childId)
    check "prompt bookkeeper agent" (str (get promptCalls.[0] "body") "agent" = "bookkeeper")
}

let run () = promise {
    sessionApiOfNullClient ()
    sessionApiOfClientWithoutSession ()
    sessionApiOfSessionWithoutCreate ()
    do! queueBackgroundLaunchWithoutApiInvokesRecordResult ()
    do! queueMuxBackgroundLaunchSuccess ()
    do! queueMuxBackgroundLaunchDelegateThrows ()
    do! launchBackgroundSessionHappyPath ()
}