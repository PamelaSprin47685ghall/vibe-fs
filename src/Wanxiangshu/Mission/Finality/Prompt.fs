namespace Wanxiangshu.Mission.Finality
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart

open System

/// GLORY-052/076 + §9.2.2–9.2.4 + SURFACE-004: Finality experience prompt owner.
/// Prose meaning lives in `resources/provider/lifecycle/finality/**` (PROMPT-019).
module FinalityPrompt =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Rejected = "lifecycle/finality/rejected"

        [<Literal>]
        let Blessed = "lifecycle/finality/blessed"

        [<Literal>]
        let Rest = "lifecycle/finality/rest"

        [<Literal>]
        let Steer = "lifecycle/finality/steer"

        [<Literal>]
        let SteerUnavailable = "lifecycle/finality/steer-unavailable"

    let private withOptionalRecord (headerDocument: string) (recordBody: string) =
        let header = headerDocument.TrimEnd('\n')
        let normalized = SyntheticToml.normalizeNewlines recordBody

        if String.IsNullOrWhiteSpace normalized then
            header + "\n"
        else
            header + "\n\n" + SyntheticToml.comment normalized + "\n"

    let blessedFromLogs (blessingHeaderDocument: string) (logs: (int * string) list) =
        let header = blessingHeaderDocument.TrimEnd('\n')

        let recordBlocks =
            logs
            |> List.sortBy fst
            |> List.choose (fun (_, content) ->
                let normalized = SyntheticToml.normalizeNewlines content

                if String.IsNullOrWhiteSpace normalized then
                    None
                else
                    Some(SyntheticToml.comment normalized))

        match recordBlocks with
        | [] -> header + "\n"
        | blocks -> header + "\n\n" + String.concat "\n\n" blocks + "\n"

    let blessed (blessingHeaderDocument: string) (workRecordBundle: string) =
        withOptionalRecord blessingHeaderDocument workRecordBundle

    let rejected (rejectionHeaderDocument: string) (reviewerWorkRecord: string) =
        withOptionalRecord rejectionHeaderDocument reviewerWorkRecord

    let steer (steerHeaderDocument: string) (siblingWorkRecord: string) =
        withOptionalRecord steerHeaderDocument siblingWorkRecord
