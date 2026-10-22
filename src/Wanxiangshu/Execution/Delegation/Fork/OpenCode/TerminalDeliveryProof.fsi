namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// The verification Host drives the real delivery workflow for one accepted child input.
module TerminalDeliveryProof =
    val materialize:
        journal: AgentJournal ->
        directory: string ->
        sessionId: SessionId ->
        physical: PhysicalUserMessageId ->
        provider: ProviderRunIdentity ->
        text: string ->
            Task
