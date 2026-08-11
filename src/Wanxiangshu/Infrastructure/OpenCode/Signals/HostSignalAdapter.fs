namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
        // LOOP-009: edge sensor callback. Typed as `obj -> unit` so this file
        // stays free of LoopSensor's Host dependency (compile order + ARCH-002
        // layering). Drop-session cleanup is the caller's job.
        ?onLoopEvent: obj -> unit
    ) =

    // Fail-closed: empty registry owns nothing.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Contains(SessionId.value sessionId)

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.UnregisterOwned(sessionId: SessionId) =
        let key = SessionId.value sessionId
        ownedSessions.Remove key |> ignore

    /// LOOP-009: text deltas first; coarse signals second; everything else drop.
    member _.Observe(raw: obj) =
        match onLoopEvent with
        | Some observe when LoopEventCodec.isLoopTextDelta raw -> observe raw
        | _ ->
            match HostSignalAdapter.tryAdapt isOwned raw with
            | Some signal -> onSignal signal
            | None -> ()

    member this.ObserveLocal(raw: obj) = this.Observe raw
