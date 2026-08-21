namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
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
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

/// EXEC-032: semantic assignment and provider bytes are deliberately distinct.
type SyncDelegatePromptRequest =
    { Charge: string
      ProviderPrompt: LlmFacing.Document }

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    let raw (charge: string) =
        { Charge = charge
          ProviderPrompt = LlmFacing.instruction charge }

    let withProviderPrompt (charge: string) (providerPrompt: LlmFacing.Document) =
        { Charge = charge
          ProviderPrompt = providerPrompt }
