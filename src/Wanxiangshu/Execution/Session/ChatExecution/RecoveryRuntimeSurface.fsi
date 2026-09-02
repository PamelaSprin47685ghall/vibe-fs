namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks

module RecoveryRuntimeSurface =
    val recoverScenarios: scenarios: string array -> Task<obj>
    val recoverAcrossRestart: scenarios: string array -> Task<obj>

    val interpretFailurePolicy:
        failureLabel: string ->
        retryBudget: string ->
        fallbackBudget: string ->
        commitment: string ->
        observation: string ->
            Task<obj>

    val admissionCrashPointScenarios:
        cuts: string array -> restartKind: string -> commitment: string -> capacityOutcome: string -> Task<obj>

    val lifecycleSignals: unit -> string array
