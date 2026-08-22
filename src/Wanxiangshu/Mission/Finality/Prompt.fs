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
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart

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

    let private withOptionalRecord (headerInstructions: string list) (recordBody: string) =
        let normalized = LlmFacing.normalizeNewlines recordBody

        if System.String.IsNullOrWhiteSpace normalized then
            LlmFacing.renderInstructions headerInstructions
        else
            LlmFacing.renderInstructions (headerInstructions @ [ normalized ])

    let blessedFromLogs (blessingHeaderInstructions: string list) (logs: (int * string) list) =
        let records =
            logs
            |> List.sortBy fst
            |> List.choose (fun (_, content) ->
                let normalized = LlmFacing.normalizeNewlines content

                if System.String.IsNullOrWhiteSpace normalized then
                    None
                else
                    Some normalized)

        LlmFacing.renderInstructions (blessingHeaderInstructions @ records)

    let blessed (blessingHeaderInstructions: string list) (workRecordBundle: string) =
        withOptionalRecord blessingHeaderInstructions workRecordBundle

    let rejected (rejectionHeaderInstructions: string list) (reviewerWorkRecord: string) =
        withOptionalRecord rejectionHeaderInstructions reviewerWorkRecord

    let steer (steerHeaderInstructions: string list) (siblingWorkRecord: string) =
        withOptionalRecord steerHeaderInstructions siblingWorkRecord
