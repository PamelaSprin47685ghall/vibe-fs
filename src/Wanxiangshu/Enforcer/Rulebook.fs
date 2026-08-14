namespace Wanxiangshu.Enforcer

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

/// One paired observation unit: optional tip identity with optional frame digest/body.
///
/// Rulebook residual vocabulary as a **domain view** only — fold of tips + frames.
/// CompanionProjectionBuilder already interleaves tip/frame provider messages with the
/// same front-zip; this type names the unit. Physical journal events are
/// `BlogObservationCommitted` / `BlogObservationsSquashed` (no second store).
type ObservationUnit =
    {
        TipName: string option
        /// Hex digest of the durable frame blob when present.
        FrameDigest: string option
        /// Optional resolved frame body (or body ref text); absent when unpaired tip.
        FrameBody: string option
    }

/// Named work-log Observation: tip identity paired with an optional frame digest.
///
/// Domain name for the rulebook BlogObservation* residual. Physical journal facts are
/// `BlogObservationCommitted` (frame + tip half) and `BlogObservationsSquashed`
/// (Observation squash: frames and tips co-truncate). Not a second projection store —
/// pair Enforcement RecentTips with Blog frames at the boundary.
type WorkLogObservation =
    { TipName: string
      CycleId: string
      FrameDigest: string option }

/// Pure tip↔frame pairing for Observation history (rulebook §2).
///
/// Not a second projection store: call sites fold RecentTips with coverable frames
/// through `pairTipsAndFrames` / `ofTipsAndFrames`. Message-layer rendering stays in
/// CompanionProjectionBuilder. Journal `ObservationProjection.observationsOf` is the
/// session-facing zip over Enforcement + Blog.
[<RequireQualifiedAccess>]
module RulebookObservation =

    /// Zip tips with frames into observation units (oldest → newest).
    ///
    /// Semantics match CompanionProjectionBuilder's private pairTipFrameUnits:
    /// while both sides remain, emit tipᵢ then frameᵢ as one unit; leftover tips or
    /// frames append unpaired. Prefer this over tips∥frames parallel streams.
    let pairTipsAndFrames (tips: string list) (frames: (string * string option) list) : ObservationUnit list =
        let rec loop tipRest frameRest acc =
            match tipRest, frameRest with
            | t :: ts, (digest, body) :: fs ->
                loop
                    ts
                    fs
                    ({ TipName = Some t
                       FrameDigest = Some digest
                       FrameBody = body }
                     :: acc)
            | t :: ts, [] ->
                loop
                    ts
                    []
                    ({ TipName = Some t
                       FrameDigest = None
                       FrameBody = None }
                     :: acc)
            | [], (digest, body) :: fs ->
                loop
                    []
                    fs
                    ({ TipName = None
                       FrameDigest = Some digest
                       FrameBody = body }
                     :: acc)
            | [], [] -> List.rev acc

        loop tips frames []

    /// Zip tip identities (TipName × CycleId) with frame digests into WorkLogObservations.
    ///
    /// Front-zip oldest → newest. Leftover tips keep `FrameDigest = None`. Leftover
    /// frames are dropped: a WorkLogObservation is tip-anchored (cycle identity required).
    /// Prefer this when projecting Enforcement RecentTips against Blog frame digests.
    let ofTipsAndFrames (tips: (string * string) list) (frameDigests: string list) : WorkLogObservation list =
        let rec loop tipRest digestRest acc =
            match tipRest, digestRest with
            | (name, cycle) :: ts, d :: ds ->
                loop
                    ts
                    ds
                    ({ TipName = name
                       CycleId = cycle
                       FrameDigest = Some d }
                     :: acc)
            | (name, cycle) :: ts, [] ->
                loop
                    ts
                    []
                    ({ TipName = name
                       CycleId = cycle
                       FrameDigest = None }
                     :: acc)
            | [], _ -> List.rev acc

        loop tips frameDigests []

    /// Lift `ObservationUnit` list that already carries tip names into WorkLogObservation
    /// when a cycle id is supplied per tip index (unpaired frames skipped).
    let workLogFromUnits (tipCycles: (string * string) list) (units: ObservationUnit list) : WorkLogObservation list =
        let digests = units |> List.choose (fun u -> u.FrameDigest)

        ofTipsAndFrames tipCycles digests
