namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module TransactionSurface =
    val transactionScenario: evidenceValue: obj -> failurePoint: string -> stateLabel: string -> Task<obj>
    val preProviderSettlementScenario: evidenceValue: obj -> failureKind: string -> releaseMode: string -> Task<obj>
