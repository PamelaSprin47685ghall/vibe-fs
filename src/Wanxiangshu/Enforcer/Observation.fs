namespace Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger

/// Observation view over Journal Blog + Enforcement substrate.
///
/// Cutover DONE: physical EventStore / journal facts ARE
/// `BlogObservationCommitted` (frame append + enforcement tip half) and
/// `BlogObservationsSquashed` (frame collapse + tip co-truncate). This module
/// names the paired fold over those facts — it does not own a second store.
///
/// Coverage stays a BlogProjection fold field (not a private coverage file).
[<RequireQualifiedAccess>]
module ObservationProjection =

    /// Frame digests oldest → newest from Blog projection (empty when absent).
    let private frameDigests (blog: BlogProjectionState option) : string list =
        match blog with
        | None -> []
        | Some state ->
            BlogProjection.frames state
            |> List.map (fun frame -> BlobDigest.value frame.Digest)

    /// Tip identities oldest → newest from Enforcement RecentTips (empty when absent).
    let private tipIdentities (enforcement: EnforcementProjectionState option) : (string * string) list =
        match enforcement with
        | None -> []
        | Some state ->
            EnforcementProjection.recentTips state
            |> List.map (fun tip -> tip.FieldName, tip.CycleId)

    /// Zip Enforcement RecentTips with Blog frames into domain WorkLogObservations.
    ///
    /// Call sites that already hold the two session halves use this instead of
    /// maintaining tips∥frames as parallel streams. Order is oldest → newest.
    let observationsOf
        (enforcement: EnforcementProjectionState option)
        (blog: BlogProjectionState option)
        : WorkLogObservation list =
        RulebookObservation.ofTipsAndFrames (tipIdentities enforcement) (frameDigests blog)

    /// Session-facing Observation view (Blog + Enforcement on one SessionAgentProjection).
    let observationsOfSession (session: SessionAgentProjection) : WorkLogObservation list =
        observationsOf session.Enforcement session.Blog

    /// After Observation squash (`BlogObservationsSquashed` + `EnforcementProjection.applySquash`),
    /// re-derive the paired list. Pure convenience over `observationsOf`.
    let observationsAfterSquash
        (coveredFrameCount: int)
        (enforcement: EnforcementProjectionState)
        (blog: BlogProjectionState)
        : WorkLogObservation list =
        let tips = EnforcementProjection.applySquash coveredFrameCount enforcement

        observationsOf (Some tips) (Some blog)
