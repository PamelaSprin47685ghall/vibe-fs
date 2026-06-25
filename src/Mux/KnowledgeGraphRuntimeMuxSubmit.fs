module VibeFs.Mux.KnowledgeGraphRuntimeMuxSubmit

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell

open VibeFs.Kernel.Messaging
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Kernel.KnowledgeGraphMaintenance
open VibeFs.Kernel.KnowledgeGraphPrompts
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphStorage
open VibeFs.Shell.KnowledgeGraphWorkflow
open VibeFs.Shell.KnowledgeGraphMaintenanceRun
open VibeFs.Shell.KnowledgeGraphBookkeeperLaunch
open VibeFs.Shell.KnowledgeGraphRuntimeTestPorts
open VibeFs.Shell.PromiseQueue
open VibeFs.Shell.ToolRuntimeContext
open VibeFs.Shell.ToolContextCodec
open VibeFs.Shell.Dyn
open VibeFs.Mux.KnowledgeGraphRuntimeIO
open VibeFs.Mux.KnowledgeGraphRuntimeMux

type MuxKnowledgeGraphRuntime with

    member this.Submit(sessionID: string, directory: string, drafts: KnowledgeGraphDraft list, ?config: obj) : JS.Promise<string> =
        if not (knowledgeGraphDirExists directory) then Promise.lift "Knowledge graph directory not found."
        else
            promise {
                match config with
                | Some cfg -> this.SetLatestConfig(Some cfg)
                | None -> ()

                let! earlyReject =
                    match this.GetChatHistory() with
                    | Some getHistory when sessionID <> "" ->
                        promise {
                            try
                                let! history = getHistory sessionID
                                let messages = MessagingCodec.decodeMessages sessionID history
                                return
                                    if historyHasCompletedReturnBookkeeper messages then
                                        Some rejectSecondReturnBookkeeperMessage
                                    else
                                        None
                            with _ ->
                                return None
                        }
                    | _ -> Promise.lift None

                match earlyReject with
                | Some msg -> return msg
                | None ->
                    let! reconstructed = this.TryResolveJobContext(sessionID)
                    let jobCtxOpt =
                        reconstructed |> Option.orElseWith (fun () -> this.TakeJob(sessionID))

                    match jobCtxOpt with
                    | None -> return "No active knowledge graph job for this session."
                    | Some ctx ->
                        let root = ctx.workspaceRoot
                        let todayStr = (this.GetNowUtc()).ToString("yyyy-MM-dd")
                        let! result = this.EnqueueWrite(fun () ->
                            promise {
                                let! entriesResult = buildEntries root drafts
                                match entriesResult with
                                | Error e -> return e
                                | Ok entries ->
                                    let! result = submitForKind root todayStr entries ctx.kind
                                    this.DeleteJob(sessionID)
                                    return result
                            })

                        match ctx.kind with
                        | DailyRewrite _ -> this.StartMaintenanceIfDue(root) |> ignore
                        | _ -> ()

                        return result
            }

    member this.StartBookkeeperAppend(prompt: string, result: string, title: string, ?config: obj) : unit =
        let root =
            match config with
            | Some cfg when not (Dyn.isNullish cfg) ->
                match fromMuxConfig cfg with
                | Ok runtime -> runtime.Execution.Directory
                | Error _ -> muxConfigDirectoryFallback cfg
            | _ -> ""
        if root = "" || not (knowledgeGraphDirExists root) then ()
        else
            this.RecordLaunch { agent = "bookkeeper"; title = title; prompt = prompt; result = result }
            match config with
            | Some cfg when not (Dyn.isNullish cfg) -> this.SetLatestConfig(Some cfg)
            | _ -> ()
            this.StartMaintenanceIfDue(root) |> ignore
            match this.GetLatestConfig() with
            | Some cfg ->
                match this.GetDeps() with
                | Some deps when not (Dyn.isNullish deps) && not (Dyn.isNullish cfg) ->
                    let sink = this.GetBackgroundSink()
                    let options = Some (box {| aiSettingsAgentId = "bookkeeper" |})
                    queueMuxBackgroundLaunch
                        deps
                        cfg
                        "bookkeeper"
                        title
                        options
                        (trackBackgroundJob sink)
                        (recordLaunchResult sink title)
                        (fun () ->
                            promise {
                                let! projection = readProjectionForRoot root
                                return
                                    prependJobMarker { workspaceRoot = root; kind = AppendAfterWork }
                                        (buildAppendPrompt title prompt result projection)
                            })
                        delegateToSubAgent
                | _ -> ()
            | None -> ()
