namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

module HandleCompletionCodec =
    val encodeOutcome: runId: string -> outcome: AgentCompletionOutcome -> string
    val decodeBody: json: string -> DurableCompletionDecode

    val tryMaterialiseRunCompletion:
        record: HandleRecord ->
        agentId: string ->
        decoded: DurableAgentCompletionV2 ->
        completedAt: DateTimeOffset ->
            RunCompletion

    val tryDecode:
        record: HandleRecord ->
        agentId: string ->
        json: string ->
        completedAt: DateTimeOffset ->
            Result<RunCompletion, string>

    val tryRead:
        journal: AgentJournalPort ->
        record: HandleRecord ->
        agentId: string ->
        completedAt: DateTimeOffset ->
            Task<Result<RunCompletion option, string>>

    val tryReadBody:
        journal: AgentJournalPort ->
        record: HandleRecord ->
            Task<Result<string option * BlobRef option * BlobDigest option, string>>
