namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Kernel.Identity

/// Speculative PrefixEpoch commit helpers for TodoCheckpoint evidence (§16.7).
///
/// When wired: generalize ContextFactCases.PrefixRebaseCommitted + ActivePrefixEpoch
/// to accept EvidenceKind = Probe | TodoCheckpoint. Until then these pure helpers
/// derive the commit payload without inventing a parallel SSOT.
module MagicTodoPrefixEpoch =

    /// desiredCutoff(Tk) covered-before id = previous Accepted (T1 → None).
    let coveredBefore (acceptedInOrder: TodoWriteId list) (trigger: TodoWriteId) : TodoWriteId option =
        let rec findPrev remaining =
            match remaining with
            | [] -> None
            | [ _ ] -> None
            | a :: b :: rest when TodoWriteId.value b = TodoWriteId.value trigger -> Some a
            | _ :: rest -> findPrev rest

        findPrev acceptedInOrder

    /// Build TodoCheckpoint EvidenceKind for the next provider attempt seal.
    let todoCheckpointEvidence
        (acceptedInOrder: TodoWriteId list)
        (trigger: TodoWriteId)
        : PrefixEvidenceKind =
        PrefixEvidenceKind.TodoCheckpoint(trigger, coveredBefore acceptedInOrder trigger)

    /// Whether a desired TodoCheckpoint rebase is mandatory after Accepted(Tk).
    /// T1 → false (no prior). Tk (k≥2) → true.
    let requiresLag1Rebase (acceptedInOrder: TodoWriteId list) : bool =
        List.length acceptedInOrder >= 2

    /// Assemble speculative V2 commit payload. Caller supplies PrefixCoverage-
    /// proven Y bundle (never LWR RawGap) and snapshot fields.
    let buildTodoCheckpointCommit
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (previousEpoch: PrefixEpochId)
        (snapshot: PrefixSnapshot)
        (acceptedInOrder: TodoWriteId list)
        (trigger: TodoWriteId)
        (yBundleRef: BlobRef)
        (yBundleDigest: BlobDigest)
        (providerPrefixDigest: string)
        : PrefixRebaseCommittedV2 =
        { SessionId = sessionId
          ManagerLifeId = Some lifeId
          PreviousEpochId = previousEpoch
          NextEpochId = PrefixEpochId.next previousEpoch
          EvidenceKind = todoCheckpointEvidence acceptedInOrder trigger
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

    /// Map legacy Probe commit shape into V2 EvidenceKind.Probe (migration aid).
    let ofLegacyProbe
        (sessionId: SessionId)
        (previousEpoch: PrefixEpochId)
        (nextEpoch: PrefixEpochId)
        (snapshot: PrefixSnapshot)
        (probeId: string)
        (solvingRun: ProviderRunIdentity)
        : PrefixRebaseCommittedV2 =
        { SessionId = sessionId
          ManagerLifeId = None
          PreviousEpochId = previousEpoch
          NextEpochId = nextEpoch
          EvidenceKind = PrefixEvidenceKind.Probe probeId
          FrozenRecordPrefixRef = snapshot.FrozenRecordPrefixRef
          FrozenRecordPrefixDigest = snapshot.FrozenRecordPrefixDigest
          CutoffExclusive = snapshot.CutoffExclusive
          CoveredPrefixDigest = snapshot.CoveredPrefixDigest
          SealRoot = snapshot.SealRoot
          SyntheticMessageId = snapshot.SyntheticMessageId
          YBundleRef = None
          YBundleDigest = None
          ProviderPrefixDigest = None
          SolvingProviderRun = Some solvingRun }
