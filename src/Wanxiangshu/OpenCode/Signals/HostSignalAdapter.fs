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
        ?onNeedHelpEvent: obj -> unit
    ) =

    // Fail-closed: empty registry owns nothing.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Contains(SessionId.value sessionId)

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.UnregisterOwned(sessionId: SessionId) =
        let key = SessionId.value sessionId
        ownedSessions.Remove key |> ignore

    /// HOST-027 part classification and LOOP-009 text detection both observe the
    /// same raw stream events. NeedHelp records part.updated before the matching
    /// delta; the composition root may then suppress reasoning deltas from the
    /// LoopSensor. Neither callback owns coarse HostSignal adaptation.
    member _.Observe(raw: obj) =
        match onNeedHelpEvent with
        | Some observe when NeedHelpEventCodec.isNeedHelpRelevantEvent raw -> observe raw
        | _ -> ()

        match onLoopEvent with
        | Some observe when LoopEventCodec.isLoopTextDelta raw -> observe raw
        | _ ->
            match HostSignalAdapter.tryAdapt isOwned raw with
            | Some signal -> onSignal signal
            | None -> ()

    member this.ObserveLocal(raw: obj) = this.Observe raw
