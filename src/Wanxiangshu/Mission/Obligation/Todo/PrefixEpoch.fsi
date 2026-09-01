namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

module MagicTodoPrefixEpoch =
    val todoCheckpointEvidence:
        trigger: TodoWriteId -> previousCommitted: TodoWriteId option -> PrefixEvidenceKind

    val requiresLag1Rebase: previousCommitted: TodoWriteId option -> bool

    val buildTodoCheckpointCommit:
        sessionId: SessionId ->
        lifeId: ManagerLifeId ->
        previousEpoch: PrefixEpochId ->
        snapshot: PrefixSnapshot ->
        previousCommitted: TodoWriteId option ->
        trigger: TodoWriteId ->
        yBundleRef: BlobRef ->
        yBundleDigest: BlobDigest ->
        providerPrefixDigest: string ->
        PrefixRebaseCommittedV2
