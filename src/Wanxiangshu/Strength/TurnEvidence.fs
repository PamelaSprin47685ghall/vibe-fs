namespace Wanxiangshu.Strength

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
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
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Composition.Turn

/// STRENGTH-007: maps Host reconciliation material onto the domain's causal
/// consumption evidence. Host bookkeeping alone is never proof that a provider
/// saw the Candidate input.
[<RequireQualifiedAccess>]
module StrengthTurnEvidence =

    let classifyParts (parts: MessagePart array) : StrengthProviderOutputEvidence =
        let classify =
            function
            | MessagePart.Text text
            | MessagePart.Reasoning text when not (String.IsNullOrWhiteSpace text) -> 2
            | MessagePart.ToolCall(callId, name, _) when
                not (String.IsNullOrWhiteSpace callId) || not (String.IsNullOrWhiteSpace name)
                ->
                2
            | MessagePart.ToolResult _
            | MessagePart.Activity _ -> 1
            | MessagePart.Text _
            | MessagePart.Reasoning _
            | MessagePart.ToolCall _ -> 0

        match parts |> Array.fold (fun strongest part -> max strongest (classify part)) 0 with
        | 2 -> StrengthProviderOutputEvidence.RealOutput
        | 1 -> StrengthProviderOutputEvidence.TransportOnly
        | _ -> StrengthProviderOutputEvidence.NoOutput

    let primarySymbol (parts: MessagePart array) : StrengthPrimarySymbol =
        let calls =
            parts
            |> Array.choose (function
                | MessagePart.ToolCall(_, name, _) -> Some name
                | _ -> None)
            |> Array.toList

        match calls with
        | _ :: _ when
            calls
            |> List.forall (fun name -> name = "read" || name = "glob" || name = "grep")
            ->
            StrengthPrimarySymbol.ReadonlyBatch
        | _ :: _ -> StrengthPrimarySymbol.MutatingOrExecuting
        | [] when
            parts
            |> Array.exists (function
                | MessagePart.Text text
                | MessagePart.Reasoning text -> not (String.IsNullOrWhiteSpace text)
                | _ -> false)
            ->
            StrengthPrimarySymbol.TextOnly
        | [] -> StrengthPrimarySymbol.Other

    let promotionDecision
        (targetProviderRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity)
        (turn: ReconciledTurn)
        : StrengthPromotionDecision =
        if targetProviderRun <> turn.ProviderRun then
            StrengthPromotionDecision.IgnoreWrongRun
        else
            match turn.Outcome with
            | ReconcileProgram.TurnCompleted
            | ReconcileProgram.TurnNeedsContinuation _ ->
                StrengthPromotion.decide targetProviderRun turn.ProviderRun (classifyParts turn.Parts)
            | ReconcileProgram.TurnFailed _
            | ReconcileProgram.TurnAborted _
            | ReconcileProgram.TurnInProgress -> StrengthPromotionDecision.AwaitOrAbandon
