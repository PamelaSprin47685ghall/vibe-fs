// primary_owner: obligation-ledger — Obligation.LedgerContract — KEEP — OpeningFloor effective floor surface
namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
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
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Obligation.Todo

/// TODO-001 / GLORY-074 / COMPANION-014: production Opening floor owner.
///
/// Derives Blogger / Companion effective floor from the true Life Opening.
/// Never reads WorkActivated / ProtectedPrefixEnd.
module ManagerOpeningFloor =

    let private partAnchors (xTrace: XTraceProjectionState) : MagicTodo.TracePartAnchor list =
        XTraceProjection.parts xTrace
        |> List.map (fun part ->
            { Cursor = part.Cursor
              Kind = part.Kind
              ToolCallId = part.ToolCallId })

    let private t1Anchor (magic: MagicTodoProjection.LifeMagicTodoState) : (XTraceCursor * ToolCallId) option =
        magic.FirstPlanCommitment
        |> Option.bind (fun writeId -> Map.tryFind (TodoWriteId.value writeId) magic.Checkpoints)
        |> Option.map (fun cp -> cp.ReviewFrontier, cp.ToolCallId)

    let private workRecordStartAfterPlan
        (life: LifeProjection)
        (state: MagicTodoProjection.LifeMagicTodoState)
        (xTrace: XTraceProjectionState)
        =
        match t1Anchor state with
        | Some(callCursor, callId) ->
            Some(MagicTodo.blindPlanOpeningBoundary life.OpeningCursor callCursor callId (partAnchors xTrace))
        | None -> Some(MagicTodo.workRecordStart life.OpeningCursor)

    /// WorkRecordStart when Post-T1; None while Pre-T1 (Opening still open).
    let workRecordStart
        (life: LifeProjection)
        (magic: MagicTodoProjection.LifeMagicTodoState option)
        (xTrace: XTraceProjectionState)
        : XTraceCursor option =
        match magic with
        | None -> None
        | Some state when not (MagicTodoProjection.isPlanCommitted state) -> None
        | Some state -> workRecordStartAfterPlan life state xTrace

    let private effectiveFloorForCurrentLife (current: LifeProjection) =
        Some(MagicTodo.workRecordStart current.OpeningCursor)

    /// Production floor for BloggerCoordinator / CompanionTransform.
    let effectiveOpeningFloor
        (life: LifeProjection option)
        (_magicTodo: MagicTodoProjection.MagicTodoProjectionState)
        (_xTrace: XTraceProjectionState)
        : XTraceCursor option =
        match life with
        | None -> None
        | Some current when current.Completed -> None
        | Some current -> effectiveFloorForCurrentLife current

    /// Session helper: CurrentLife floor sequence for Blogger max(coverage, floor).
    let floorSequence (sessionId: SessionId) (projections: AgentProjectionSet) : int64 option =
        let session = AgentProjection.tryFind sessionId projections

        let life =
            session
            |> Option.bind (fun s -> s.ManagerLife)
            |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

        let xTrace =
            session
            |> Option.bind (fun s -> s.XTrace)
            |> Option.defaultValue XTraceProjection.empty

        effectiveOpeningFloor life projections.MagicTodo xTrace
        |> Option.map (fun cursor -> cursor.Sequence)
