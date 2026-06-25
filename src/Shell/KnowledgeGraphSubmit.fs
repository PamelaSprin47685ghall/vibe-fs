module VibeFs.Shell.KnowledgeGraphSubmit

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphStorage

let buildEntriesFromDrafts (root: string) (drafts: KnowledgeGraphDraft list)
    : JS.Promise<Result<KnowledgeGraphEntry list, string>> =
    promise {
        let! projection = readProjectionForRoot root
        let normalizedDrafts = normalizeDraftIds projection drafts
        let random = System.Random()
        return applyDrafts (allocateRandomHexId (fun () -> random.Next(0, 65536))) projection normalizedDrafts
    }

let submitEntriesForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int)
    (root: string) (todayStr: string)
    (entries: KnowledgeGraphEntry list) (kind: KnowledgeGraphJobKind)
    : JS.Promise<string> =
    promise {
        let! result =
            match kind with
            | AppendAfterWork ->
                appendDrafts portLockTimeoutMs portLockRetryDelayMs root todayStr entries
            | DailyRewrite date ->
                rewriteDayUnderLock portLockTimeoutMs portLockRetryDelayMs root date entries
        match result with
        | Error e -> return e
        | Ok () ->
            match kind with
            | AppendAfterWork -> return $"Appended {entries.Length} knowledge graph entries."
            | DailyRewrite date -> return $"Rewrote knowledge graph day {date}."
    }

let submitDraftsForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int)
    (root: string) (todayStr: string)
    (drafts: KnowledgeGraphDraft list) (kind: KnowledgeGraphJobKind)
    : JS.Promise<string> =
    promise {
        let! entriesResult = buildEntriesFromDrafts root drafts
        match entriesResult with
        | Error e -> return e
        | Ok entries -> return! submitEntriesForKind portLockTimeoutMs portLockRetryDelayMs root todayStr entries kind
    }
