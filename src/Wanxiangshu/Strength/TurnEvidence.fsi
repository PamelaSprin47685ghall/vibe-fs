namespace Wanxiangshu.Strength

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Replica

/// STRENGTH-007: maps Host reconciliation material onto the domain's causal
/// consumption evidence. Host bookkeeping alone is never proof that a provider
/// saw the Candidate input.
[<RequireQualifiedAccess>]
module StrengthTurnEvidence =
    val classifyParts: parts: MessagePart array -> StrengthProviderOutputEvidence
    val primarySymbol: parts: MessagePart array -> StrengthPrimarySymbol

    val promotionDecision: targetProviderRun: ProviderRunIdentity -> turn: ReconciledTurn -> StrengthPromotionDecision
