namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks

module RecoveryRuntimeSurface =
    val recoverScenarios: scenarios: string array -> Task<obj>

    val interpretFailurePolicy:
        failureLabel: string -> retryBudget: string -> commitment: string -> observation: string -> Task<obj>

    val admissionPhaseSamples: cuts: string array -> commitment: string -> capacityOutcome: string -> Task<obj>
