namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Obligation.Todo

type DelegationHandoffWindow =
    { Range: MagicTodoLwr.BoundedRange
      IsInitial: bool }

type PreparedDelegationHandoff =
    { ParentRecord: string option
      ParentEndExclusive: XTraceCursor }

[<RequireQualifiedAccess>]
module DelegationHandoff =

    let key (parent: SessionId) (delegateSession: SessionId) =
        SessionId.value parent + "\x1f" + SessionId.value delegateSession

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

    let renderPrompt (charge: string) (parentRecord: string option) =
        let body =
            parentRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun record ->
                [ SyntheticToml.field "parent_delta_work_record" (SyntheticToml.renderString record) ])
            |> Option.defaultValue []

        SyntheticToml.document [ charge ] body

    /// Append parent delta to an already-rendered provider prompt without
    /// reinterpreting or re-rendering the existing bytes. Warm-start and other
    /// typed provider documents keep their own instruction/data structure.
    let appendParentDelta (providerPrompt: string) (parentRecord: string option) =
        match
            parentRecord
            |> Option.map (fun record -> record.Trim())
            |> Option.filter (System.String.IsNullOrWhiteSpace >> not)
        with
        | None -> providerPrompt
        | Some record ->
            let appendix =
                SyntheticToml.document
                    []
                    [ SyntheticToml.field "parent_delta_work_record" (SyntheticToml.renderString record) ]

            providerPrompt.TrimEnd() + "\n\n" + appendix
