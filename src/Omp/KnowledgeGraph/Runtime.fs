module VibeFs.Omp.KnowledgeGraph.Runtime

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Omp.KnowledgeGraphRuntimeIO
open VibeFs.Shell.Dyn
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.PromiseQueue

module Dyn = VibeFs.Shell.Dyn

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

    member this.EnsureSessionSnapshot(sessionID: string, directory: string) : JS.Promise<KnowledgeGraphProjection> =
        VibeFs.Omp.KnowledgeGraph.Snapshot.ensureSessionSnapshot
            commandQueue
            (fun () -> state)
            applyCmd
            sessionID
            directory
            effectiveRoot

    member _.RegisterJob(sessionID: string, ctx: KnowledgeGraphJobContext) : unit =
        registeredJobs <- Map.add sessionID ctx registeredJobs

    member _.DeleteJob(sessionID: string) : unit =
        registeredJobs <- Map.remove sessionID registeredJobs

    member this.Submit(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
        VibeFs.Omp.KnowledgeGraph.Submit.submit
            magicGetEntries.Value
            this.TryResolveJobContext
            (fun () -> registeredJobs)
            (fun newJobs -> registeredJobs <- newJobs)
            runWorkspace
            effectiveRoot
            this.StartMaintenanceIfDue
            sessionID
            directory
            drafts
            today
            knowledgeGraphDirExists

    member this.BuildPreludeForSession(sessionID: string, directory: string) : JS.Promise<string option> =
        VibeFs.Omp.KnowledgeGraph.Snapshot.buildPreludeForSession
            (fun sid dir -> this.EnsureSessionSnapshot(sid, dir))
            sessionID
            directory
            knowledgeGraphDirExists

    member this.FetchFromSessionSnapshot(sessionID: string, directory: string, entity: string) : JS.Promise<string> =
        VibeFs.Omp.KnowledgeGraph.Fetch.fetchFromSessionSnapshot
            (fun sid dir -> this.EnsureSessionSnapshot(sid, dir))
            sessionID
            directory
            entity
            knowledgeGraphDirExists

    member this.StartMaintenanceIfDue(workspaceRoot: string) : JS.Promise<unit> =
        VibeFs.Omp.KnowledgeGraph.Maintenance.startMaintenanceIfDue
            commandQueue
            (fun () -> state)
            (fun newState -> state <- newState)
            (fun p r t w -> this.StartBookkeeperAppend(p, r, t, w))
            workspaceRoot
            effectiveRoot
            knowledgeGraphDirExists

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, workspaceRoot: string) : unit =
        VibeFs.Omp.KnowledgeGraph.Maintenance.startBookkeeperAppend
            pi
            applyCmd
            recordBackgroundResult
            startBackgroundJob
            (fun sid ctx -> this.RegisterJob(sid, ctx))
            (fun w -> this.StartMaintenanceIfDue(w))
            prompt
            result
            title
            workspaceRoot
            effectiveRoot
            knowledgeGraphDirExists

    member _.RegisterJobForTesting(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
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
        registeredJobs <- Map.add sessionID { workspaceRoot = workspaceRoot; kind = kind } registeredJobs

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
