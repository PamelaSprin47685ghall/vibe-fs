module VibeFs.Opencode.KnowledgeGraphRuntimeIO

open Fable.Core
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraphRuntimeState
open VibeFs.Shell.KnowledgeGraphSubmit

let buildEntries = VibeFs.Shell.KnowledgeGraphSubmit.buildEntriesFromDrafts

let submitForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int) (todayStr: string) (root: string) (kind: KnowledgeGraphJobKind) (drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
    VibeFs.Shell.KnowledgeGraphSubmit.submitDraftsForKind portLockTimeoutMs portLockRetryDelayMs root todayStr drafts kind

let tryResolveJobContext = VibeFs.Opencode.KnowledgeGraphSessionMessages.tryResolveJobContext

let loadSessionMessages = VibeFs.Opencode.KnowledgeGraphSessionMessages.loadSessionMessages
