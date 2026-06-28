module Wanxiangshu.Omp.KnowledgeGraph.Maintenance

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Maintenance
open Wanxiangshu.Kernel.KnowledgeGraph.Prompts
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell

module Dyn = Wanxiangshu.Shell.Dyn

let private bookkeeperChildTools = [| "read"; "return_bookkeeper" |]

let startMaintenanceIfDue
    (commandQueue: SerialQueue)
    (getState: unit -> KnowledgeGraphState)
    (setState: KnowledgeGraphState -> unit)
    (startBookkeeperCallback: string -> string -> string -> string -> unit)
    (workspaceRoot: string)
    (effectiveRoot: string -> string)
    (kgDirExists: string -> bool)
    : JS.Promise<unit> =
    let root = effectiveRoot workspaceRoot
    if root = "" || not (kgDirExists root) then Promise.lift ()
    else
        commandQueue.Enqueue(fun () ->
            promise {
                let! files = readKnowledgeGraphFiles root
                let projection = projectLatestWins files
                let dailyDue = dueMaintenance files (Clock.nowUtc ())
                let launchIfDue due kind title resultPrefix promptInfix buildPrompt =
                    due
                    |> List.iter (fun value ->
                        let key = root + "|" + resultPrefix + "|" + value
                        let launch =
                            { agent = "bookkeeper"
                              title = title
                              prompt = $"{resultPrefix} maintenance due {promptInfix} {value}"
                              result = $"{resultPrefix}:{value}" }
                        let first, nextState = recordLaunchOnce (getState()) key launch
                        setState nextState
                        if first then
                            let promptText =
                                prependJobMarker { workspaceRoot = root; kind = kind value }
                                    (buildPrompt value files projection)
                            startBookkeeperCallback promptText $"{resultPrefix}:{value}" title root)
                launchIfDue dailyDue DailyRewrite "Daily knowledge graph rewrite" "daily" "for" buildDailyPrompt
            })

let startBookkeeperAppend
    (pi: obj)
    (applyCmd: KnowledgeGraphCommand -> unit)
    (recordBackgroundResult: string -> string -> unit)
    (startBackgroundJob: JS.Promise<unit> -> unit)
    (registerJob: string -> KnowledgeGraphJobContext -> unit)
    (startMaintenanceCallback: string -> JS.Promise<unit>)
    (prompt: string)
    (result: string)
    (title: string)
    (workspaceRoot: string)
    (effectiveRoot: string -> string)
    (kgDirExists: string -> bool)
    : unit =
    let root = effectiveRoot workspaceRoot
    if root = "" || not (kgDirExists root) then ()
    else
        applyCmd (RecordLaunchCmd { agent = "bookkeeper"; title = title; prompt = prompt; result = result })
        startMaintenanceCallback root |> ignore
        promise {
            try
                let ctx = createObj [ "cwd", box root ]
                let! child = createChildSession pi ctx bookkeeperChildTools None [||] None
                let sm = Dyn.get child.session "sessionManager"
                let childId =
                    let sid = Dyn.str child.session "id"
                    if sid <> "" then sid else Dyn.str sm "sessionId"
                registerJob childId { workspaceRoot = root; kind = AppendAfterWork }
                do! child.session?prompt(prompt) |> unbox<JS.Promise<unit>>
                recordBackgroundResult title "success"
                child.dispose |> Option.iter (fun d -> d ())
            with ex ->
                recordBackgroundResult title (string ex)
        }
        |> startBackgroundJob
