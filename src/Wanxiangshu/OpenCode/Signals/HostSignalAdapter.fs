namespace Wanxiangshu.OpenCode

open Wanxiangshu.Interaction.Dispatch.OpenCode

open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Applies ownership and source routing to HostEventCodec output. Raw host
/// payload decoding has exactly one owner: HostEventCodec.
module HostSignalAdapter =

    let sessionIdOf =
        function
        | SessionIdle sessionId
        | AttemptAborted sessionId -> sessionId
        | SessionDeleted(sessionId, _) -> sessionId
        | ProviderRetry retry -> retry.SessionId
        | ProviderFailure(sessionId, _) -> sessionId

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
        // LOOP-009 / HOST-027 edge callbacks. Typed as `obj -> unit` so this
        // adapter routes raw Host events without owning sensor business state.
        // Drop-session cleanup is the composition root's job.
        ?onLoopEvent: obj -> unit,
        ?onNeedHelpEvent: obj -> unit,
        ?onProviderStepEnd: SessionId -> PhysicalUserMessageId -> ProviderRunIdentity -> unit,
        ?onPhysicalExecutionEnd: SessionId -> PhysicalUserMessageId -> unit
    ) =

    // Fail-closed: empty registry owns nothing.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Contains(SessionId.value sessionId)

    let emitAdaptedSignal raw =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | Some signal -> onSignal signal
        | None -> ()

    let observePhysicalExecutionEnd raw =
        match onPhysicalExecutionEnd, HostEventCodec.tryDecodePhysicalExecutionEnd raw with
        | Some observe, Some(sessionId, physicalUserMessageId) -> observe sessionId physicalUserMessageId
        | _ -> ()

    let observeProviderStepEnd raw =
        match onProviderStepEnd, HostEventCodec.tryDecodeProviderStepEnd raw with
        | Some observe, Some(sessionId, physicalUserMessageId, providerRun) ->
            observe sessionId physicalUserMessageId providerRun
        | _ -> ()

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.UnregisterOwned(sessionId: SessionId) =
        let key = SessionId.value sessionId
        ownedSessions.Remove key |> ignore

    /// HOST-027 part classification and LOOP-009 text detection both observe the
    /// same raw stream events. NeedHelp records part.updated before the matching
    /// delta. LoopSensor observes all textual and reasoning stream deltas. Model
    /// routing also observes exact terminal assistant identity; none of these
    /// callbacks turns a fragment into a business HostSignal.
    member _.Observe(raw: obj) =
        observeProviderStepEnd raw
        observePhysicalExecutionEnd raw

        match onNeedHelpEvent with
        | Some observe when NeedHelpEventCodec.isNeedHelpRelevantEvent raw -> observe raw
        | _ -> ()

        match onLoopEvent with
        | Some observe when LoopEventCodec.isLoopTextDelta raw -> observe raw
        | _ -> emitAdaptedSignal raw

    member this.ObserveLocal(raw: obj) = this.Observe raw
