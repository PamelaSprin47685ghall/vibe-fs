namespace Wanxiangshu.OpenCode.Host

open Wanxiangshu.OpenCode

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Fission

/// Restart drops every process-local execution binding. The durable handle
/// projection is the evidence that outlives it: which child session belongs to
/// which parent, and with which execution agent. This module installs that
/// evidence as the fallback behind `SessionExecutionBinding`'s in-process cache,
/// so no load-order step has to pre-populate anything — resolution happens on
/// demand, and a missing binding is never again indistinguishable from a missing
/// child (crash-reconciliation-020, managed-session-lifecycle-024).
module SessionBindingRecovery =

    let private agentNameOf (record: HandleRecord) : string =
        let target =
            if isNull record.TargetAgent then
                ""
            else
                record.TargetAgent.Trim()

        let byname = if isNull record.Byname then "" else record.Byname.Trim()
        // The execution agent is what the Host dispatches with (`engineer`), never
        // the logical reuse address (`decision-record-readonly`); binding the
        // byname would fail the next dispatch on participant drift.
        if not (System.String.IsNullOrWhiteSpace target) then
            target
        else
            byname

    let private parentOwningHandle (projections: AgentProjectionSet) (handle: HandleId) : SessionId option =
        projections.Sessions
        |> Map.toList
        |> List.tryPick (fun (parentSessionId, session) ->
            match session.Handles with
            | Some handles when HandleProjection.tryFind handle handles |> Option.isSome -> Some parentSessionId
            | _ -> None)

    /// Durable evidence for one child session: its parent and its execution agent.
    /// Only parent-visible durable handles answer; Host-owned hidden leaves stay
    /// invisible to the parent's binding surface.
    let evidenceFor (projections: AgentProjectionSet) (childSessionId: SessionId) : (string * string) option =
        // 1. Check parent-visible durable handles
        projections.HandleByChildSession
        |> Map.tryFind childSessionId
        |> Option.filter (fun record -> record.Ownership = HandleOwnership.DurableParentHandle)
        |> Option.bind (fun record ->
            parentOwningHandle projections record.Handle
            |> Option.map (fun parentSessionId -> SessionId.value parentSessionId, agentNameOf record))
        |> Option.orElseWith (fun () ->
            // 2. Check durable Companion session associations
            SessionAssociationProjection.tryMainSessionOf childSessionId projections.Associations
            |> Option.map (fun parentSessionId -> SessionId.value parentSessionId, "blogger"))

    /// Install the resolver over any projection source (the journal in production,
    /// a folded projection under test). The source is read on demand, so a later
    /// append is visible without reinstalling.
    let installFrom (projections: unit -> AgentProjectionSet) : unit =
        SessionExecutionBinding.installDurableChildEvidence (fun sessionKey ->
            evidenceFor (projections ()) (SessionId.create sessionKey))

    /// Durable evidence for one fission lane: which owner and slot it belongs to.
    /// The fission projection is the truth; the lane registry is the cache of the
    /// lanes this process is currently driving.
    let fissionLaneFor (projections: AgentProjectionSet) (laneSessionId: SessionId) : FissionLaneBinding option =
        let state = projections.Fission

        FissionProjection.tryMembershipOfLane laneSessionId state
        |> Option.bind (fun (group, laneIndex) ->
            FissionProjection.tryOwnerOfLane laneSessionId state
            |> Option.map (fun owner ->
                { GroupId = group.GroupId
                  OwnerSessionId = owner
                  LaneIndex = laneIndex
                  LaneCount = group.LaneCount }))

    /// Load Phase: install every durable resolver behind the process caches.
    let install (journal: AgentJournal) : unit =
        let projections () =
            (AgentJournal.snapshot journal).AgentProjections

        installFrom projections
        FissionRuntime.installDurableLaneEvidence (fun laneSessionId -> fissionLaneFor (projections ()) laneSessionId)
