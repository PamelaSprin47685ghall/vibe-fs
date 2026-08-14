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
      ProviderPrompt: string }

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-031).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    /// Idle nudge when a SyncDelegate turn fails without completing — no return tool.
    let IdleNudge = "delegation/sync-idle"

    let raw (charge: string) =
        { Charge = charge
          ProviderPrompt = charge }

    let withProviderPrompt (charge: string) (providerPrompt: string) =
        { Charge = charge
          ProviderPrompt = providerPrompt }

    let idleNudgeDocument (instructionLines: string list) =
        SyntheticToml.document instructionLines []
