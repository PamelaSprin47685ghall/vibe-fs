module Wanxiangshu.Opencode.KnowledgeGraphRuntimeIO

open Fable.Core
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.KnowledgeGraphSubmit

let buildEntries = Wanxiangshu.Shell.KnowledgeGraphSubmit.buildEntriesFromDrafts

let submitForKind (portLockTimeoutMs: int64) (portLockRetryDelayMs: int) (todayStr: string) (root: string) (kind: KnowledgeGraphJobKind) (drafts: KnowledgeGraphDraft list) : JS.Promise<string> =
    Wanxiangshu.Shell.KnowledgeGraphSubmit.submitDraftsForKind portLockTimeoutMs portLockRetryDelayMs root todayStr drafts kind

let tryResolveJobContext = Wanxiangshu.Opencode.KnowledgeGraphSessionMessages.tryResolveJobContext

let loadSessionMessages = Wanxiangshu.Opencode.KnowledgeGraphSessionMessages.loadSessionMessages
