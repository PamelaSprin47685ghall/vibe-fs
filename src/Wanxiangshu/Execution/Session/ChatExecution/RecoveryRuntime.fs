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

    let interpret (ports: ChatExecutionRecoveryActionPorts) (decision: ChatExecutionRecoveryDecision) : Task =
        match decision with
        | ChatExecutionRecoveryDecision.Ignore _ -> Task.FromResult(()) :> Task
        | ChatExecutionRecoveryDecision.ReconcilePhysical request -> ports.ReconcilePhysical request
        | ChatExecutionRecoveryDecision.ResumePreProvider request -> ports.ResumePreProvider request
        | ChatExecutionRecoveryDecision.RequeueEligible request -> ports.RequeueEligible request
        | ChatExecutionRecoveryDecision.Finalize request -> ports.Finalize request
        | ChatExecutionRecoveryDecision.MarkManualIntervention request -> ports.MarkManualIntervention request

    let recover
        (ports: ChatExecutionRecoveryActionPorts)
        (evidence: ChatExecutionRecoveryEvidence)
        : Task<ChatExecutionRecoveryDecision> =
        task {
            let decision = ChatExecutionRecovery.decide evidence
            do! interpret ports decision
            return decision
        }
