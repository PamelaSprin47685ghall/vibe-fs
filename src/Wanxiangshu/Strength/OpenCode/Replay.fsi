namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.Foundation

open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence

/// STRENGTH-008: Host-boundary Strength replay into the provider-facing
/// transcript, and Promoted→Traced close after XTrace capture.
/// Lifecycle math stays in StrengthLifecycle / StrengthTraceRecovery;
/// this module only wires Host messages, projection render, and durability.
[<RequireQualifiedAccess>]
module StrengthReplay =
    val applyBeforeXTrace:
        journal: AgentJournal option ->
        snapshotPort: Wanxiangshu.OpenCode.ISessionSnapshotPort option ->
        strengthDurability: StrengthDurabilityPort option ->
        strengthFailFuse: (string -> unit) ->
        ownerRole: (string -> Role option) ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task<StrengthReplayPlan list>

    val commitTracedAfterCapture:
        journal: AgentJournal option ->
        strengthDurability: StrengthDurabilityPort option ->
        strengthFailClosed: (string -> unit) ->
        traceState: XTraceProjectionState option ->
        plans: StrengthReplayPlan list ->
            Task
