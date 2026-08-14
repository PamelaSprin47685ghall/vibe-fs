namespace Wanxiangshu.Strength.Replica
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

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

type StrengthTraceObservedPart =
    { CursorSequence: int64
      Kind: string
      ToolName: string option
      Body: string }

[<RequireQualifiedAccess>]
module StrengthTraceRecovery =

    let expectedParts (bundle: StrengthFrameBundle) : (string * string option * string) list =
        bundle.Batches
        |> List.collect (fun batch ->
            let calls =
                batch.Exchanges
                |> List.map (fun exchange -> "tool_call", Some exchange.ToolName, exchange.CanonicalArguments)

            let results =
                batch.Exchanges
                |> List.map (fun exchange -> "tool_result", None, exchange.CanonicalResult)

            calls @ results)

    /// Recover the exact XTrace range after a crash between XTrace append and the
    /// StrengthFramesTraced append. Matching is semantic because XTrace correctly
    /// discarded cross-session wire call ids. Exactly one contiguous match is
    /// accepted; ambiguity is a fail-closed durability condition.
    let recoverRange
        (bundle: StrengthFrameBundle)
        (observed: StrengthTraceObservedPart list)
        : Result<StrengthTraceRange option, string> =
        let expected = expectedParts bundle

        if List.isEmpty expected then
            Ok None
        else
            let width = List.length expected

            let matchesAt index =
                let window = observed |> List.skip index |> List.truncate width

                if List.length window <> width then
                    false
                else
                    List.zip window expected
                    |> List.forall (fun (actual, (kind, toolName, body)) ->
                        actual.Kind = kind && actual.ToolName = toolName && actual.Body = body)

            let matches =
                [ 0 .. max -1 (List.length observed - width) ] |> List.filter matchesAt

            match matches with
            | [] -> Ok None
            | [ index ] ->
                let window = observed |> List.skip index |> List.truncate width
                let first = List.head window

                let contiguous =
                    window
                    |> List.mapi (fun offset part -> part.CursorSequence = first.CursorSequence + int64 offset)
                    |> List.forall id

                if not contiguous then
                    Error "Strength XTrace match is not a contiguous cursor range"
                else
                    let last = List.last window

                    Ok(
                        Some
                            { StartInclusive = first.CursorSequence
                              EndExclusive = last.CursorSequence + 1L }
                    )
            | _ -> Error "Strength XTrace recovery is ambiguous"
