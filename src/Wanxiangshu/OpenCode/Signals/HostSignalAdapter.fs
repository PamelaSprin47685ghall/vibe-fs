namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session.ChatExecution

/// Applies ownership and source routing to HostEventCodec output. Raw host
/// payload decoding has exactly one owner: HostEventCodec.
module HostSignalAdapter =

    let sessionIdOf =
        function
        | SessionIdle sessionId -> sessionId
        | AttemptAborted failure -> failure.SessionId
        | SessionDeleted(sessionId, _) -> sessionId
        | ProviderRetry retry -> retry.SessionId
        | ProviderFailure failure -> failure.SessionId

    /// SSOT signals are session.status idle|retry and session.deleted.
    let tryAdapt (isOwned: SessionId -> bool) (rawInput: obj) : HostSignal option =
        HostEventCodec.tryDecode rawInput
        |> Option.bind (fun signal ->
            if
                isOwned (sessionIdOf signal)
                || (match signal with
                    | ProviderFailure _ -> true
                    | _ -> false)
            then
                Some signal
            else
                None)

type HostSignalRouter
    (
        ownedSessions: HashSet<string>,
        onSignal: HostSignal -> unit,
        ?onExactAssistantObservation:
            ExactProviderStartObservation -> bool -> ExactProviderTerminalObservation option -> Task<unit>
    ) =

    // Fail-closed: empty registry owns nothing.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Contains(SessionId.value sessionId)

    let emitAdaptedSignal raw =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | Some signal -> onSignal signal
        | None -> ()

    let observeExactAssistant raw : Task<unit> =
        match onExactAssistantObservation, HostEventCodec.tryDecodeExactProviderStart raw with
        | Some observe, Some started ->
            let terminal =
                HostEventCodec.tryDecodeExactProviderTerminal raw
                |> Option.filter (fun completed ->
                    completed.SessionId = started.SessionId
                    && completed.PhysicalUserMessageId = started.PhysicalUserMessageId
                    && completed.ProviderRun = started.ProviderRun)

            let providerStepEnded =
                HostEventCodec.tryDecodeProviderStepEnd raw
                |> Option.exists (fun (sessionId, physicalUserMessageId, providerRun) ->
                    sessionId = started.SessionId
                    && physicalUserMessageId = started.PhysicalUserMessageId
                    && providerRun = started.ProviderRun)

            observe started providerStepEnded terminal
        | _ -> Task.FromResult()

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.UnregisterOwned(sessionId: SessionId) =
        let key = SessionId.value sessionId
        ownedSessions.Remove key |> ignore

    /// The SDK subscription carries coarse wakeups only. Exact assistant
    /// lifecycle evidence is owned by the awaited public event Hook below.
    member _.Observe(raw: obj) = emitAdaptedSignal raw

    member _.ObserveLocal(raw: obj) =
        task {
            do! observeExactAssistant raw
            emitAdaptedSignal raw
        }
