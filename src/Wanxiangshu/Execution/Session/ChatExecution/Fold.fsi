namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact

[<RequireQualifiedAccess>]
module ChatExecutionFactFold =
    val applyAccepted:
        key: ChatExecutionKey ->
        evidence: AcceptedChatExecutionEvidence ->
        projection: ChatExecutionProjectionState ->
            Result<ChatExecutionProjectionState, FoldRejection>

    val applyProviderStarted:
        key: ChatExecutionKey ->
        evidence: ProviderStartedEvidence ->
        projection: ChatExecutionProjectionState ->
            Result<ChatExecutionProjectionState, FoldRejection>

    val applyTerminal:
        key: ChatExecutionKey ->
        evidence: ChatExecutionTerminalEvidence ->
        disposition: ChatExecutionTerminalDisposition ->
        projection: ChatExecutionProjectionState ->
            Result<ChatExecutionProjectionState, FoldRejection>

    val fold:
        projection: ChatExecutionProjectionState ->
        fact: ChatExecutionFactCases ->
            Result<ChatExecutionProjectionState, FoldRejection>
