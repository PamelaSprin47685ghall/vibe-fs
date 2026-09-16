namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ChatExecutionRecoveryLifecycleEvent =
    | DurabilityActivated
    | PluginRuntimeReloaded
    | ExactAssistantStarted of ProviderStartedEvidence
    | ExactAssistantTerminal of ProviderStartedEvidence * ChatExecutionTerminalDisposition
    | SessionAborted of ChatExecutionKey
    | SessionDeleted of ChatExecutionKey
    | SessionCancelled of ChatExecutionKey
    | CapacityProjectionReplayed

    /// PAR-023：宿主发布 session idle 后，本 session 的 `Accepted` 且无
    /// `ProviderStarted` 的执行是确切义务集：宿主已停止执行它们，恢复必须
    /// 逐一定夺（resume 或终态），不得留成悬空态。
    | SessionQuiesced of SessionId

type ChatExecutionRecoveryActionPorts =
    { ReconcilePhysical: PhysicalReconciliationRequest -> Task
      ResumePreProvider: PreProviderResumeRequest -> Task
      Finalize: TerminalFinalizationRequest -> Task
      MarkManualIntervention: ManualInterventionRequest -> Task }

[<RequireQualifiedAccess>]
module ChatExecutionRecoveryRuntime =
    val interpret: ports: ChatExecutionRecoveryActionPorts -> decision: ChatExecutionRecoveryDecision -> Task

    val recover:
        ports: ChatExecutionRecoveryActionPorts ->
        evidence: ChatExecutionRecoveryEvidence ->
            Task<ChatExecutionRecoveryDecision>
