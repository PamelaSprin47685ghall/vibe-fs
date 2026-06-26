module VibeFs.Omp.KnowledgeGraph.Snapshot

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.Prompts
open VibeFs.Kernel.KnowledgeGraph.RuntimeState
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.PromiseQueue

let ensureSessionSnapshot
    (commandQueue: SerialQueue)
    (getState: unit -> KnowledgeGraphState)
    (applyCmd: KnowledgeGraphCommand -> unit)
    (sessionID: string)
    (directory: string)
    (effectiveRoot: string -> string)
    : JS.Promise<KnowledgeGraphProjection> =
    if sessionID = "" then Promise.lift Map.empty
    else
        let root = effectiveRoot directory
        if root = "" then Promise.lift Map.empty
        else
            commandQueue.Enqueue(fun () ->
                promise {
                    match Map.tryFind sessionID (getState()).sessionSnapshots with
                    | Some projection -> return projection
                    | None ->
                        let! projection = readProjection root
                        applyCmd (CacheSnapshotCmd (sessionID, projection))
                        return projection
                })

let buildPreludeForSession
    (ensureSnapshot: string -> string -> JS.Promise<KnowledgeGraphProjection>)
    (sessionID: string)
    (directory: string)
    (kgDirExists: string -> bool)
    : JS.Promise<string option> =
    promise {
        if not (kgDirExists directory) then return None
        else
            let! projection = ensureSnapshot sessionID directory
            return buildPreludeSection projection
    }
