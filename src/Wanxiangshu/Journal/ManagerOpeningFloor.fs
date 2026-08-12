namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Identity

/// TODO-001 / GLORY-074 / COMPANION-014: production Opening floor owner.
///
/// Derives Blogger / Companion effective floor from Life + MagicTodo + XTrace.
/// Never reads WorkActivated / ProtectedPrefixEnd.
module ManagerOpeningFloor =

    let private partAnchors (xTrace: XTraceProjectionState) : MagicTodo.TracePartAnchor list =
        xTrace.Parts
        |> List.map (fun part ->
            { Cursor = part.Cursor
              Kind = part.Kind
              ToolCallId = part.ToolCallId })

    let private t1Anchor (magic: MagicTodoProjection.LifeMagicTodoState) : (XTraceCursor * ToolCallId) option =
        match magic.AcceptedOrder with
        | [] -> None
        | firstId :: _ ->
            match Map.tryFind (TodoWriteId.value firstId) magic.Checkpoints with
            | Some cp -> Some(cp.ReviewFrontier, cp.ToolCallId)
            | None -> None

    /// WorkRecordStart when Post-T1; None while Pre-T1 (Opening still open).
    let workRecordStart
        (life: LifeProjection)
        (magic: MagicTodoProjection.LifeMagicTodoState option)
        (xTrace: XTraceProjectionState)
        : XTraceCursor option =
        match magic with
        | None -> None
        | Some state when List.isEmpty state.AcceptedOrder -> None
        | Some state ->
            match t1Anchor state with
            | Some(callCursor, callId) ->
                Some(MagicTodo.blindPlanOpeningBoundary life.OpeningCursor callCursor callId (partAnchors xTrace))
            | None -> Some(MagicTodo.workRecordStart life.OpeningCursor)

    /// Production floor for BloggerCoordinator / CompanionTransform.
    let effectiveOpeningFloor
        (life: LifeProjection option)
        (magicTodo: MagicTodoProjection.MagicTodoProjectionState)
        (xTrace: XTraceProjectionState)
        : XTraceCursor option =
        match life with
        | None -> None
        | Some current when current.Completed -> None
        | Some current ->
            let magic = MagicTodoProjection.tryLife current.LifeId magicTodo

            let acceptedCount =
                magic
                |> Option.map (fun state -> List.length state.AcceptedOrder)
                |> Option.defaultValue 0

            let callCursor, callId =
                match magic |> Option.bind t1Anchor with
                | Some(cursor, id) -> Some cursor, Some id
                | None -> None, None

            MagicTodo.effectiveOpeningFloor
                true
                acceptedCount
                current.OpeningCursor
                callCursor
                callId
                (XTraceProjection.headSequence xTrace)
                (partAnchors xTrace)

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
