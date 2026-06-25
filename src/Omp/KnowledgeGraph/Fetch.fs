module VibeFs.Omp.KnowledgeGraph.Fetch

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Shell.KnowledgeGraphFiles

let fetchFromSessionSnapshot
    (ensureSnapshot: string -> string -> JS.Promise<KnowledgeGraphProjection>)
    (sessionID: string)
    (directory: string)
    (entity: string)
    (kgDirExists: string -> bool)
    : JS.Promise<string> =
    promise {
        if not (kgDirExists directory) then return "Knowledge graph directory not found."
        elif sessionID = "" then return "Knowledge graph snapshot unavailable for this session."
        else
            let! projection = ensureSnapshot sessionID directory
            match fetchAnswer projection entity with
            | Ok answer -> return answer
            | Error message -> return message
    }
