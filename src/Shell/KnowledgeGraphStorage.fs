module VibeFs.Shell.KnowledgeGraphStorage

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.KnowledgeGraphPortLock

let defaultPortLockTimeoutMs = 30000L
let defaultPortLockRetryDelayMs = 1000

let readProjectionForRoot (workspaceRoot: string) : JS.Promise<KnowledgeGraphProjection> =
    readProjection workspaceRoot

let serializedWrite
    (timeoutMs: int64)
    (retryDelayMs: int)
    (workspaceRoot: string)
    (work: unit -> JS.Promise<'a>)
    : JS.Promise<Result<'a, string>> =
    withKnowledgeGraphPortLock timeoutMs retryDelayMs workspaceRoot work

let appendEntriesUnderLock
    (timeoutMs: int64)
    (retryDelayMs: int)
    (workspaceRoot: string)
    (today: string)
    (entries: KnowledgeGraphEntry list)
    : JS.Promise<Result<unit, string>> =
    serializedWrite timeoutMs retryDelayMs workspaceRoot (fun () -> appendEntries workspaceRoot today entries)

let rewriteDayUnderLock
    (timeoutMs: int64)
    (retryDelayMs: int)
    (workspaceRoot: string)
    (date: string)
    (entries: KnowledgeGraphEntry list)
    : JS.Promise<Result<unit, string>> =
    serializedWrite timeoutMs retryDelayMs workspaceRoot (fun () -> rewriteDay workspaceRoot date entries)

let appendDrafts
    (timeoutMs: int64)
    (retryDelayMs: int)
    (workspaceRoot: string)
    (today: string)
    (entries: KnowledgeGraphEntry list)
    : JS.Promise<Result<unit, string>> =
    appendEntriesUnderLock timeoutMs retryDelayMs workspaceRoot today entries