namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Foundation.Identity

/// Speculative PrefixEpoch commit helpers for TodoCheckpoint evidence (§16.7).
///
/// When wired: generalize ContextFactCases.PrefixRebaseCommitted + ActivePrefixEpoch
/// to accept EvidenceKind = Probe | TodoCheckpoint. Until then these pure helpers
/// derive the commit payload without inventing a parallel SSOT.
module MagicTodoPrefixEpoch =

    /// Build TodoCheckpoint EvidenceKind from the O(1) committed predecessor
    /// locator maintained by MagicTodoProjection. T1 has no predecessor.
    let todoCheckpointEvidence (trigger: TodoWriteId) (previousCommitted: TodoWriteId option) : PrefixEvidenceKind =
        PrefixEvidenceKind.TodoCheckpoint(trigger, previousCommitted)

    /// Whether a desired TodoCheckpoint rebase exists for this committed checkpoint.
    let requiresLag1Rebase (previousCommitted: TodoWriteId option) : bool = previousCommitted.IsSome

    /// Assemble speculative V2 commit payload. Caller supplies PrefixCoverage-
    /// proven Y bundle (never LWR RawGap) and snapshot fields.
    let buildTodoCheckpointCommit
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (previousEpoch: PrefixEpochId)
        (snapshot: PrefixSnapshot)
        (previousCommitted: TodoWriteId option)
        (trigger: TodoWriteId)
        (yBundleRef: BlobRef)
        (yBundleDigest: BlobDigest)
        (providerPrefixDigest: string)
        : PrefixRebaseCommittedV2 =
        { SessionId = sessionId
          ManagerLifeId = Some lifeId
          PreviousEpochId = previousEpoch
          NextEpochId = PrefixEpochId.next previousEpoch
          EvidenceKind = todoCheckpointEvidence trigger previousCommitted
          FrozenRecordPrefixRef = snapshot.FrozenRecordPrefixRef
          FrozenRecordPrefixDigest = snapshot.FrozenRecordPrefixDigest
          CutoffExclusive = snapshot.CutoffExclusive
          CoveredPrefixDigest = snapshot.CoveredPrefixDigest
          SealRoot = snapshot.SealRoot
          SyntheticMessageId = snapshot.SyntheticMessageId
          YBundleRef = Some yBundleRef
          YBundleDigest = Some yBundleDigest
          ProviderPrefixDigest = Some providerPrefixDigest
          SolvingProviderRun = None }
