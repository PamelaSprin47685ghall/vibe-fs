namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module Distillation =
    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val FragmentPrompt: string = "tool/distill/fragment-prompt"

        [<Literal>]
        val InputTruncated: string = "tool/distill/input-truncated"

        [<Literal>]
        val CondensationFailed: string = "tool/distill/condensation-failed"

    type IDistillationRuntime = DistillationRuntime.IDistillationRuntime

    val asDistillationRuntime:
        (HostForkRuntime -> AgentJournal -> DistillationRuntime.RequirePermit -> IDistillationRuntime)

    val ofForkRuntime: (ForkRuntime -> IDistillationRuntime)
    val distillFragmentPrompt: lang: ProviderLanguage -> string

    [<Literal>]
    val AwaitAgentTimeoutMs: int = 600000

    val awaitAgentWithPermit: runtime: IDistillationRuntime -> agentId: string -> Task<RunCompletion>
    val distillSpool: runtime: IDistillationRuntime -> spoolPath: string -> lang: ProviderLanguage -> Task<string>
