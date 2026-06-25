module VibeFs.Omp.KnowledgeGraphRuntime

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.Messaging
open VibeFs.Omp.ChildSession
open VibeFs.Omp.KnowledgeGraphRuntimeIO
open VibeFs.Omp.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.Dyn

module Dyn = VibeFs.Shell.Dyn

let private bookkeeperChildTools = [| "read"; "return_bookkeeper" |]

type OmpKnowledgeGraphRuntime(pi: obj) =
    let mutable state = initialKnowledgeGraphState
    let commandQueue = SerialQueue()
    let backgroundJobs = ResizeArray<JS.Promise<unit>>()
    let mutable writeQueues = Map.empty<string, SerialQueue>
    let mutable registeredJobs = Map.empty<string, KnowledgeGraphJobContext>
    let magicGetEntries = ref None

    let today () = System.DateTime.UtcNow.ToString("yyyy-MM-dd")

    let applyCmd (cmd: KnowledgeGraphCommand) = state <- reducer state cmd

    let recordBackgroundResult title result =
        applyCmd (UpdateLatestLaunchResultCmd (title, result))

    let startBackgroundJob (job: JS.Promise<unit>) =
        backgroundJobs.Add(job)
        job |> Promise.start

    let effectiveRoot (value: string) : string =
        if System.String.IsNullOrWhiteSpace value then "" else value

    let getWorkspaceQueue (root: string) =
        match Map.tryFind root writeQueues with
        | Some queue -> queue
        | None ->
            let queue = SerialQueue()
            writeQueues <- Map.add root queue writeQueues
            queue

    let runWorkspace (root: string) (work: unit -> JS.Promise<string>) : JS.Promise<string> =
        getWorkspaceQueue root |> fun queue -> queue.Enqueue(work)

    member _.BindGetEntries(load: unit -> obj array) : unit =
        magicGetEntries.Value <- Some load

    member _.TryResolveJobContext(sessionID: string) : JS.Promise<KnowledgeGraphJobContext option> =
        tryResolveJobContext magicGetEntries.Value sessionID ""

    member _.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<KnowledgeGraphProjection> =
        if sessionID = "" then Promise.lift Map.empty
        else
            let root = effectiveRoot directory
            if root = "" then Promise.lift Map.empty
            else
                commandQueue.Enqueue(fun () ->
                    promise {
                        match Map.tryFind sessionID state.sessionSnapshots with
                        | Some projection -> return projection
                        | None ->
                            let! projection = readProjection root
                            applyCmd (CacheSnapshotCmd (sessionID, projection))
                            return projection
                    })

    member _.RegisterJob(sessionID: string, ctx: KnowledgeGraphJobContext) : unit =
        registeredJobs <- Map.add sessionID ctx registeredJobs

    member _.DeleteJob(sessionID: string) : unit =
        registeredJobs <- Map.remove sessionID registeredJobs

    member this.Submit(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
        if not (knowledgeGraphDirExists directory) then Promise.lift "Knowledge graph directory not found."
        else
            promise {
                let history =
                    match magicGetEntries.Value with
                    | Some load -> decodeEntries sessionID (load ())
                    | None -> []
                if historyHasCompletedReturnBookkeeper history then
                    return rejectSecondReturnBookkeeperMessage
                else
                    let! reconstructed = this.TryResolveJobContext(sessionID)
                    let jobCtxOpt =
                        reconstructed |> Option.orElseWith (fun () -> Map.tryFind sessionID registeredJobs)
                    match jobCtxOpt with
                    | None -> return "No active knowledge graph job for this session."
                    | Some ctx ->
                        let root = effectiveRoot ctx.workspaceRoot
                        let! result =
                            runWorkspace root (fun () ->
                                promise {
                                    let! entriesResult = buildEntries root drafts
                                    match entriesResult with
                                    | Error e -> return e
                                    | Ok entries ->
                                        let! msg = submitForKind root (today ()) entries ctx.kind
                                        registeredJobs <- Map.remove sessionID registeredJobs
                                        return msg
                                })
                        match ctx.kind with
                        | DailyRewrite _ -> this.StartMaintenanceIfDue(root) |> ignore
                        | _ -> ()
                        return result
            }

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        promise {
            if not (knowledgeGraphDirExists directory) then return None
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                return buildPreludeSection projection
        }

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, entity: string) : JS.Promise<string> =
        promise {
            if not (knowledgeGraphDirExists directory) then return "Knowledge graph directory not found."
            elif sessionID = "" then return "Knowledge graph snapshot unavailable for this session."
            else
                let! projection = this.EnsureSessionSnapshot(sessionID, directory)
                match fetchAnswer projection entity with
                | Ok answer -> return answer
                | Error message -> return message
        }

    member this.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        let root = effectiveRoot workspaceRoot
        if root = "" || not (knowledgeGraphDirExists root) then Promise.lift ()
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    let! files = readKnowledgeGraphFiles root
                    let projection = projectLatestWins files
                    let dailyDue = dueMaintenance files System.DateTime.UtcNow
                    let launchIfDue due kind title resultPrefix promptInfix buildPrompt =
                        due
                        |> List.iter (fun value ->
                            let key = root + "|" + resultPrefix + "|" + value
                            let launch =
                                { agent = "bookkeeper"
                                  title = title
                                  prompt = $"{resultPrefix} maintenance due {promptInfix} {value}"
                                  result = $"{resultPrefix}:{value}" }
                            let first, nextState = recordLaunchOnce state key launch
                            state <- nextState
                            if first then
                                let promptText =
                                    prependJobMarker { workspaceRoot = root; kind = kind value }
                                        (buildPrompt value files projection)
                                this.StartBookkeeperAppend(promptText, $"{resultPrefix}:{value}", title, root))
                    launchIfDue dailyDue DailyRewrite "Daily knowledge graph rewrite" "daily" "for" buildDailyPrompt
                })

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, workspaceRoot: string) : unit =
        let root = effectiveRoot workspaceRoot
        if root = "" || not (knowledgeGraphDirExists root) then ()
        else
            applyCmd (RecordLaunchCmd { agent = "bookkeeper"; title = title; prompt = prompt; result = result })
            this.StartMaintenanceIfDue(root) |> ignore
            promise {
                try
                    let ctx = createObj [ "cwd", box root ]
                    let! child = createChildSession pi ctx bookkeeperChildTools None [||]
                    let sm = Dyn.get child.session "sessionManager"
                    let childId =
                        let sid = Dyn.str child.session "id"
                        if sid <> "" then sid else Dyn.str sm "sessionId"
                    this.RegisterJob(childId, { workspaceRoot = root; kind = AppendAfterWork })
                    do! child.session?prompt(prompt) |> unbox<JS.Promise<unit>>
                    recordBackgroundResult title "success"
                    child.dispose |> Option.iter (fun d -> d ())
                with ex ->
                    recordBackgroundResult title (string ex)
            }
            |> startBackgroundJob

    member this.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        let payload : obj = payload
        let readRequiredField fieldName =
            let value = Dyn.str payload fieldName
            if value.Trim() = "" then failwith $"Knowledge graph job payload missing required field '{fieldName}'"
            else value.Trim()
        let kind =
            match kindTag.Trim().ToLowerInvariant() with
            | "append" -> AppendAfterWork
            | "daily" -> DailyRewrite(readRequiredField "date")
            | other -> failwith $"Unknown knowledge graph job kind: {other}"
        this.RegisterJob(sessionID, { workspaceRoot = workspaceRoot; kind = kind })

    member _.TakeBookkeeperLaunchesForTesting() : obj array =
        let launches, nextState = drainLaunches state
        state <- nextState
        launches
        |> List.map (fun l ->
            box (createObj [ "agent", box l.agent; "title", box l.title; "prompt", box l.prompt; "result", box l.result ]))
        |> List.toArray

    member _.SnapshotRegisteredJobsForTesting() : (string * string) array =
        registeredJobs
        |> Map.toArray
        |> Array.map (fun (sid, ctx) -> sid, ctx.workspaceRoot)

    member _.WaitForBackgroundJobsForTesting() : JS.Promise<unit> =
        promise {
            do! commandQueue.Enqueue(fun () -> Promise.lift ())
            let jobs = backgroundJobs |> Seq.toArray
            backgroundJobs.Clear()
            if jobs.Length > 0 then
                let! _ = Promise.all jobs
                ()
        }