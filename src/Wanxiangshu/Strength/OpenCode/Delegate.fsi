namespace Wanxiangshu.Strength.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence

/// DELEGATE-7: Host boundary wiring for explicit read-only delegation.
/// Capture and start use the completed owner batch and final outbound mirror.
/// All policy math stays in Domain.
[<RequireQualifiedAccess>]
module StrengthDelegate =

    type CaptureOutcome =
        | Captured of DelegationRequest
        | Skipped of reason: string

    /// 机器可判的稳定捕获结果标识（非散文文案，严禁使用 Fable 内部反射）
    val captureOutcomeCode: outcome: CaptureOutcome -> string

    /// Phase one: freeze one authorization from the owner's completed source
    /// batch and persist DelegationRequested. The predictor configuration is a
    /// caller-provided existence input, independent of capacity and health.
    val tryCapture:
        snapshotPort: ISessionSnapshotPort option ->
        journal: AgentJournal option ->
        strengthDurability: StrengthDurabilityPort option ->
        strengthScope: PluginStrengthScope ->
        tryAttemptPlan: (SessionId -> ProviderRunIdentity -> AttemptPlan option) ->
        syncDelegateRuntime: SyncDelegateRuntime option ->
        predictorConfigured: bool ->
        projectionSessionIdOpt: string option ->
        timerPort: Wanxiangshu.Foundation.ITimerPort option ->
        output: obj ->
            Task<CaptureOutcome>

    /// Capture and start in one call at the end of the transform, persisting
    /// Requested and Bound before replica execution and Prepared before rendering.
    val tryCaptureAndStart:
        snapshotPort: ISessionSnapshotPort option ->
        journal: AgentJournal option ->
        strengthDurability: StrengthDurabilityPort option ->
        strengthScope: PluginStrengthScope ->
        tryAttemptPlan: (SessionId -> ProviderRunIdentity -> AttemptPlan option) ->
        syncDelegateRuntime: SyncDelegateRuntime option ->
        predictorConfigured: bool ->
        projectionSessionIdOpt: string option ->
        timerPort: Wanxiangshu.Foundation.ITimerPort option ->
        output: obj ->
            Task<unit>
