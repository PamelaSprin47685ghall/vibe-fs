module VibeFs.Omp.KnowledgeGraph.Submit

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.RuntimeState
open VibeFs.Kernel.Messaging
open VibeFs.Omp.KnowledgeGraphRuntimeIO
open VibeFs.Omp.MessagingCodec
open VibeFs.Shell.KnowledgeGraphFiles

let submit
    (magicGetEntries: (unit -> obj array) option)
    (tryResolveJob: string -> JS.Promise<KnowledgeGraphJobContext option>)
    (getRegisteredJobs: unit -> Map<string, KnowledgeGraphJobContext>)
    (setRegisteredJobs: Map<string, KnowledgeGraphJobContext> -> unit)
    (runWorkspace: string -> (unit -> JS.Promise<string>) -> JS.Promise<string>)
    (effectiveRoot: string -> string)
    (startMaintenanceIfDueCallback: string -> JS.Promise<unit>)
    (sessionID: string)
    (directory: string)
    (drafts: KnowledgeGraphDraft list)
    (today: unit -> string)
    (kgDirExists: string -> bool)
    : JS.Promise<string> =
    if not (kgDirExists directory) then Promise.lift "Knowledge graph directory not found."
    else
        promise {
            let history =
                match magicGetEntries with
                | Some load -> decodeEntries sessionID (load ())
                | None -> []
            if historyHasCompletedReturnBookkeeper history then
                return rejectSecondReturnBookkeeperMessage
            else
                let! reconstructed = tryResolveJob sessionID
                let jobCtxOpt =
                    reconstructed |> Option.orElseWith (fun () -> Map.tryFind sessionID (getRegisteredJobs()))
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
                                    setRegisteredJobs (Map.remove sessionID (getRegisteredJobs()))
                                    return msg
                            })
                    match ctx.kind with
                    | DailyRewrite _ -> startMaintenanceIfDueCallback root |> ignore
                    | _ -> ()
                    return result
        }
