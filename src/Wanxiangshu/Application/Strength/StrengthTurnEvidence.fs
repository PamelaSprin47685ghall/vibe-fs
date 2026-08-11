namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain

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
        (targetProviderRun: Wanxiangshu.Kernel.Identity.ProviderRunIdentity)
        (turn: ReconciledTurn)
        : StrengthPromotionDecision =
        StrengthPromotion.decide targetProviderRun turn.ProviderRun (classifyParts turn.Parts)
