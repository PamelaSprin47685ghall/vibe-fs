namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution

/// JS-native host signal boundary. Raw host payloads enter once; snapshots of
/// coarse wake signals leave, with no transport union representation exposed.
module HostSignalSurface =
    let private failureLabel =
        function
        | ExecutionFailure.LocalInvariant -> "LocalInvariant"
        | ExecutionFailure.ProtocolRejection -> "ProtocolRejection"
        | ExecutionFailure.AuthorizationDenied -> "AuthorizationDenied"
        | ExecutionFailure.UserCancelled -> "UserCancelled"
        | ExecutionFailure.Superseded -> "Superseded"
        | ExecutionFailure.CapacityQueueFull -> "CapacityQueueFull"
        | ExecutionFailure.ProviderTransient -> "ProviderTransient"
        | ExecutionFailure.ProviderPermanent -> "ProviderPermanent"
        | ExecutionFailure.AcceptanceUnknown -> "AcceptanceUnknown"
        | ExecutionFailure.StreamInterruptedAfterFirstToken -> "StreamInterruptedAfterFirstToken"
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.NotCommitted -> "PersistenceFailure(NotCommitted)"
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Committed -> "PersistenceFailure(Committed)"
        | ExecutionFailure.PersistenceFailure PersistenceCommitment.Unknown -> "PersistenceFailure(Unknown)"

    let private snapshot signal : obj =
        match signal with
        | HostSignal.SessionIdle sessionId ->
            box
                {| kind = "SessionIdle"
                   sessionId = SessionId.value sessionId |}
        | HostSignal.AttemptAborted failure ->
            box
                {| kind = "AttemptAborted"
                   sessionId = SessionId.value failure.SessionId
                   failure = failureLabel failure.Failure
                   diagnostic = failure.Diagnostic |}
        | HostSignal.SessionDeleted(sessionId, parent) ->
            box
                {| kind = "SessionDeleted"
                   sessionId = SessionId.value sessionId
                   parentSessionId = parent |> Option.map SessionId.value |> Option.defaultValue "" |}
        | HostSignal.ProviderRetry retry ->
            box
                {| kind = "ProviderRetry"
                   sessionId = SessionId.value retry.SessionId
                   attempt = retry.Attempt
                   failure = failureLabel retry.Failure
                   diagnostic = retry.Diagnostic |}
        | HostSignal.ProviderFailure failure ->
            box
                {| kind = "ProviderFailure"
                   sessionId = SessionId.value failure.SessionId
                   failure = failureLabel failure.Failure
                   diagnostic = failure.Diagnostic |}

    let tryDecode (raw: obj) : obj =
        HostEventCodec.tryDecode raw |> Option.map snapshot |> Option.defaultValue null

    let tryDecodePhysicalExecutionEnd (raw: obj) : obj =
        HostEventCodec.tryDecodePhysicalExecutionEnd raw
        |> Option.map (fun (sessionId, physicalUserMessageId) ->
            box
                {| sessionId = SessionId.value sessionId
                   physicalUserMessageId = PhysicalUserMessageId.value physicalUserMessageId |})
        |> Option.defaultValue null

    let tryDecodeExactProviderStart (raw: obj) : obj =
        HostEventCodec.tryDecodeExactProviderStart raw
        |> Option.map (fun observation ->
            box
                {| sessionId = SessionId.value observation.SessionId
                   physicalUserMessageId = PhysicalUserMessageId.value observation.PhysicalUserMessageId
                   providerRun = ProviderRunIdentity.value observation.ProviderRun |})
        |> Option.defaultValue null

    let tryDecodeExactProviderTerminal (raw: obj) : obj =
        HostEventCodec.tryDecodeExactProviderTerminal raw
        |> Option.map (fun observation ->
            let outcome, failure =
                match observation.Outcome with
                | HostProviderTerminalOutcome.Completed HostProviderCompletion.Stop -> "Stop", ""
                | HostProviderTerminalOutcome.Completed HostProviderCompletion.Length -> "Length", ""
                | HostProviderTerminalOutcome.Completed HostProviderCompletion.ContentFiltered -> "ContentFiltered", ""
                | HostProviderTerminalOutcome.Cancelled typedFailure -> "Cancelled", failureLabel typedFailure
                | HostProviderTerminalOutcome.Interrupted typedFailure -> "Interrupted", failureLabel typedFailure
                | HostProviderTerminalOutcome.ProviderFailure typedFailure ->
                    "ProviderFailure", failureLabel typedFailure

            let disposition =
                observation.Disposition
                |> Option.map (function
                    | ChatExecutionTerminalDisposition.Completed -> "Completed"
                    | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
                    | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
                    | ChatExecutionTerminalDisposition.Failed -> "Failed")
                |> Option.defaultValue ""

            box
                {| sessionId = SessionId.value observation.SessionId
                   physicalUserMessageId = PhysicalUserMessageId.value observation.PhysicalUserMessageId
                   providerRun = ProviderRunIdentity.value observation.ProviderRun
                   outcome = outcome
                   failure = failure
                   disposition = disposition |})
        |> Option.defaultValue null

    let tryDecodeProviderStepEnd (raw: obj) : obj =
        HostEventCodec.tryDecodeProviderStepEnd raw
        |> Option.map (fun (sessionId, physicalUserMessageId, providerRun) ->
            box
                {| sessionId = SessionId.value sessionId
                   physicalUserMessageId = PhysicalUserMessageId.value physicalUserMessageId
                   providerRun = ProviderRunIdentity.value providerRun |})
        |> Option.defaultValue null

    let tryAdapt (owned: string array) (raw: obj) : obj =
        // DSL-MUTABLE: resource — owned signal registry for host signal adaptation
        let registry = HashSet<string>(owned)

        HostSignalAdapter.tryAdapt (fun sessionId -> registry.Contains(SessionId.value sessionId)) raw
        |> Option.map snapshot
        |> Option.defaultValue null
