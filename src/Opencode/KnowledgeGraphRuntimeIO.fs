module VibeFs.Opencode.KnowledgeGraphRuntimeIO

open System
open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Opencode.KnowledgeGraphSessionMessages
open VibeFs.Shell.KnowledgeGraphStorage

let buildEntries (root: string) (drafts: KnowledgeGraphDraft list) : JS.Promise<Result<KnowledgeGraphEntry list, string>> =
    promise {
        let! projection = readProjectionForRoot root
        let normalizedDrafts = normalizeDraftIds projection drafts
        let random = Random()
        return applyDrafts (allocateRandomHexId (fun () -> random.Next(0, 65536))) projection normalizedDrafts
    }

let submitForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int) (todayStr: string) (root: string) (kind: KnowledgeGraphJobKind) (drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
    promise {
        let! entriesResult = buildEntries root drafts
        match entriesResult with
        | Error e -> return e
        | Ok entries ->
            let! lockResult =
                match kind with
                | AppendAfterWork ->
                    appendDrafts portLockTimeoutMs portLockRetryDelayMs root todayStr entries
                | DailyRewrite date ->
                    rewriteDayUnderLock portLockTimeoutMs portLockRetryDelayMs root date entries
            match lockResult with
            | Error e -> return e
            | Ok () ->
                match kind with
                | AppendAfterWork -> return $"Appended {entries.Length} knowledge graph entries."
                | DailyRewrite date -> return $"Rewrote knowledge graph day {date}."
    }

let tryResolveJobContext = VibeFs.Opencode.KnowledgeGraphSessionMessages.tryResolveJobContext

let loadSessionMessages = VibeFs.Opencode.KnowledgeGraphSessionMessages.loadSessionMessages