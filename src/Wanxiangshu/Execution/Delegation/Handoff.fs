// primary_owner: delegation — Delegation.HandoffSurface (DELEG-024/026) — KEEP — Handoff ledger & linkage, caller Fork/Host/Runtime via surface
namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Obligation.Todo

type DelegationHandoffWindow =
    { Range: MagicTodoLwr.BoundedRange
      IsInitial: bool }

type PreparedDelegationHandoff =
    { Route: DelegationHandoffRoute
      ParentStartInclusive: XTraceCursor
      ParentRecord: string option
      ParentEndExclusive: XTraceCursor }

type ReusableHandoffPort =
    { Prepare: SessionId -> DelegationHandoffRoute -> Task<PreparedDelegationHandoff>
      CheckpointCompleted: SessionId -> PreparedDelegationHandoff -> Task<Result<unit, string>> }

[<RequireQualifiedAccess>]
module DelegationHandoff =

    let key (parent: SessionId) (route: DelegationHandoffRoute) =
        SessionId.value parent + "\x1f" + DelegationHandoffRoute.value route

    let window (previousEnd: int64 option) (currentEnd: int64) : DelegationHandoffWindow =
        let start = previousEnd |> Option.defaultValue 0L

        if currentEnd < start then
            invalidArg "currentEnd" "delegation handoff cursor cannot retreat"

        { Range =
            { StartInclusive = { Sequence = start }
              EndExclusive = { Sequence = currentEnd } }
          IsInitial = previousEnd.IsNone }

    let childRange (startInclusive: int64) (endExclusive: int64) : MagicTodoLwr.BoundedRange =
        if endExclusive < startInclusive then
            invalidArg "endExclusive" "delegation child range cannot retreat"

        { StartInclusive = { Sequence = startInclusive }
          EndExclusive = { Sequence = endExclusive } }

    let promptDocument (charge: string) (parentRecord: string option) =
        let body =
            parentRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun record -> [ LlmFacing.Data.stringField "parent_delta_work_record" record ])
            |> Option.defaultValue []

        LlmFacing.instruction charge |> LlmFacing.withData body

    let renderPrompt (charge: string) (parentRecord: string option) =
        promptDocument charge parentRecord |> LlmFacing.render

    /// Append parent delta to an already-rendered provider prompt without
    /// reinterpreting or re-rendering the existing bytes. Warm-start and other
    /// typed provider documents keep their own instruction/data structure.
    let appendParentDelta (providerPrompt: LlmFacing.Document) (parentRecord: string option) =
        match
            parentRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        with
        | None -> providerPrompt
        | Some record ->
            providerPrompt
            |> LlmFacing.withData [ LlmFacing.Data.stringField "parent_delta_work_record" record ]
