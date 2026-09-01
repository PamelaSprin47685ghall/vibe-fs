namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure

[<RequireQualifiedAccess>]
type ChatExecutionRecoveryLifecycleEvent =
    | DurabilityActivated
    | PluginRuntimeReloaded
    | ExactAssistantStarted of ProviderStartedEvidence
    | ExactAssistantTerminal of ProviderStartedEvidence * ChatExecutionTerminalDisposition
    | SessionAborted of ChatExecutionKey
    | SessionDeleted of ChatExecutionKey
    | SessionCancelled of ChatExecutionKey
    | TypedFailureDecision of ChatExecutionKey * ExecutionFailureDecision
    | CapacityProjectionReplayed

type ChatExecutionRecoveryActionPorts =
    { ReconcilePhysical: PhysicalReconciliationRequest -> Task
      ResumePreProvider: PreProviderResumeRequest -> Task
      RequeueEligible: ProviderRequeueRequest -> Task
      Finalize: TerminalFinalizationRequest -> Task
      MarkManualIntervention: ManualInterventionRequest -> Task }

[<RequireQualifiedAccess>]
module ChatExecutionRecoveryRuntime =
    val interpret: ports: ChatExecutionRecoveryActionPorts -> decision: ChatExecutionRecoveryDecision -> Task
    val recover:
        ports: ChatExecutionRecoveryActionPorts ->
        evidence: ChatExecutionRecoveryEvidence ->
        Task<ChatExecutionRecoveryDecision>
