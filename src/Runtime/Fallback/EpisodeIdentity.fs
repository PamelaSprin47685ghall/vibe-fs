module Wanxiangshu.Runtime.Fallback.EpisodeIdentity

/// Unified episode identity + late-event disposition for continuation / nudge / compaction.
/// All settle/cancel/late-idle/assistant gates must share these pure rules.
/// No special-case fields outside FallbackSessionRuntime aggregate.

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime

type EpisodeKind =
    | ContinuationEpisode
    | NudgeEpisode
    | CompactionEpisode

/// Generational epoch that must match session for an event to mutate state.
type EpisodeEpoch =
    { SessionGeneration: int
      CancelGeneration: int
      HumanTurnId: string }

/// Correlation key for one logical episode (one prompt-owning run).
type EpisodeIdentity =
    { Kind: EpisodeKind
      EpisodeId: string
      Ordinal: int
      Epoch: EpisodeEpoch }

[<RequireQualifiedAccess>]
type LateEventDisposition =
    | Accept
    | DiscardStaleEpoch
    | DiscardIdMismatch
    | DiscardNoActiveEpisode

let sessionEpoch (s: FallbackSessionRuntime) : EpisodeEpoch =
    { SessionGeneration = s.SessionGeneration
      CancelGeneration = s.CancelGeneration
      HumanTurnId = s.HumanTurnId }

let activeContinuationEpoch (s: FallbackSessionRuntime) : EpisodeEpoch =
    { SessionGeneration = s.ActiveContinuationGen
      CancelGeneration = s.ActiveContinuationCancelGen
      HumanTurnId = s.HumanTurnId }

let isEpochStale (claimed: EpisodeEpoch) (current: EpisodeEpoch) : bool =
    claimed.SessionGeneration < current.SessionGeneration
    || claimed.CancelGeneration < current.CancelGeneration

let epochMatches (a: EpisodeEpoch) (b: EpisodeEpoch) : bool =
    a.SessionGeneration = b.SessionGeneration
    && a.CancelGeneration = b.CancelGeneration
    && a.HumanTurnId = b.HumanTurnId

let ofContinuationLease (lease: PendingLease) : EpisodeIdentity =
    { Kind = ContinuationEpisode
      EpisodeId = lease.ContinuationID
      Ordinal = lease.ContinuationOrdinal
      Epoch =
        { SessionGeneration = lease.SessionGeneration
          CancelGeneration = lease.CancelGeneration
          HumanTurnId = lease.HumanTurnID } }

let ofNudgeLease (lease: NudgeLease) : EpisodeIdentity =
    { Kind = NudgeEpisode
      EpisodeId = lease.NudgeID
      Ordinal = lease.NudgeOrdinal
      Epoch =
        { SessionGeneration = lease.SessionGeneration
          CancelGeneration = lease.CancelGeneration
          HumanTurnId = lease.HumanTurnID } }

let tryOfCompaction (s: FallbackSessionRuntime) : EpisodeIdentity option =
    if s.CompactionActiveId = "" then
        None
    else
        Some
            { Kind = CompactionEpisode
              EpisodeId = s.CompactionActiveId
              Ordinal = s.CompactionActiveOrdinal
              Epoch =
                { SessionGeneration = s.SessionGeneration
                  CancelGeneration = s.CompactionCancelGeneration
                  HumanTurnId = s.CompactionHumanTurnId } }

let tryCurrentContinuation (s: FallbackSessionRuntime) : EpisodeIdentity option =
    s.PendingLease |> Option.map ofContinuationLease

let tryCurrentNudge (s: FallbackSessionRuntime) : EpisodeIdentity option =
    s.PendingNudgeLease |> Option.map ofNudgeLease

let private activeOfKind (kind: EpisodeKind) (s: FallbackSessionRuntime) : EpisodeIdentity option =
    match kind with
    | ContinuationEpisode -> tryCurrentContinuation s
    | NudgeEpisode -> tryCurrentNudge s
    | CompactionEpisode -> tryOfCompaction s

/// Claimed identity still binds the live episode of the same kind.
let matchesActive (claimed: EpisodeIdentity) (s: FallbackSessionRuntime) : bool =
    match activeOfKind claimed.Kind s with
    | None -> false
    | Some active ->
        active.EpisodeId = claimed.EpisodeId
        && active.Ordinal = claimed.Ordinal
        && epochMatches active.Epoch claimed.Epoch
        && epochMatches claimed.Epoch (sessionEpoch s)

let disposeLateEvent (claimed: EpisodeIdentity) (s: FallbackSessionRuntime) : LateEventDisposition =
    let current = sessionEpoch s

    if isEpochStale claimed.Epoch current then
        LateEventDisposition.DiscardStaleEpoch
    else
        match activeOfKind claimed.Kind s with
        | None -> LateEventDisposition.DiscardNoActiveEpisode
        | Some active when active.EpisodeId <> claimed.EpisodeId -> LateEventDisposition.DiscardIdMismatch
        | Some active when not (epochMatches active.Epoch claimed.Epoch) -> LateEventDisposition.DiscardStaleEpoch
        | Some _ when not (epochMatches claimed.Epoch current) -> LateEventDisposition.DiscardStaleEpoch
        | Some _ -> LateEventDisposition.Accept

let isAccepted (d: LateEventDisposition) : bool =
    match d with
    | LateEventDisposition.Accept -> true
    | _ -> false

/// Session-scoped late busy/idle/error/assistant without a full EpisodeIdentity payload.
/// Preserves prior contId-first ordering: mismatch discards even NewUserMessage.
let disposeContinuationSessionEvent
    (eventContIdMatch: bool)
    (eventTurnIdOpt: string option)
    (isNewUserMessage: bool)
    (isAbortError: bool)
    (lifecycleCancelled: bool)
    (s: FallbackSessionRuntime)
    : LateEventDisposition =
    if not eventContIdMatch then
        LateEventDisposition.DiscardIdMismatch
    elif isNewUserMessage then
        LateEventDisposition.Accept
    else
        let current = sessionEpoch s
        let active = activeContinuationEpoch s

        let turnStale =
            match eventTurnIdOpt with
            | Some tid when tid <> "" && tid <> current.HumanTurnId -> true
            | _ -> false

        if isAbortError then
            if turnStale || isEpochStale active current then
                LateEventDisposition.DiscardStaleEpoch
            else
                LateEventDisposition.Accept
        elif lifecycleCancelled || isEpochStale active current then
            LateEventDisposition.DiscardStaleEpoch
        else
            LateEventDisposition.Accept

/// Compaction settle allowed only for the live compaction episode id + non-stale epoch.
let canSettleCompaction (expectedCompactionId: string) (s: FallbackSessionRuntime) : bool =
    match tryOfCompaction s with
    | Some ep when ep.EpisodeId = expectedCompactionId -> isAccepted (disposeLateEvent ep s)
    | _ -> false

/// Continuation lease still owns the session under unified epoch rules.
let continuationLeaseIsCurrent (lease: PendingLease) (s: FallbackSessionRuntime) : bool =
    matchesActive (ofContinuationLease lease) s
    && lease.Owner = SessionOwner.Fallback
    && s.Owner = SessionOwner.Fallback
    && s.Core.Lifecycle = FallbackLifecycle.Active

/// Nudge lease still owns the session under unified epoch rules.
let nudgeLeaseIsCurrent (lease: NudgeLease) (s: FallbackSessionRuntime) : bool =
    matchesActive (ofNudgeLease lease) s
    && lease.Owner = SessionOwner.Nudge
    && s.Owner = SessionOwner.Nudge
    && s.Core.Lifecycle = FallbackLifecycle.Active
