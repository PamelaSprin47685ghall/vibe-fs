namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity

/// Applies ownership and source routing to HostEventCodec output. Raw host
/// payload decoding has exactly one owner: HostEventCodec.
module HostSignalAdapter =

    let sessionIdOf =
        function
        | SessionIdle sessionId
        | SessionDeleted sessionId -> sessionId
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
    ) as this =
    let sources = Dictionary<string, SessionSignalSource>()

    // Fail-closed: empty registry owns nothing.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Contains(SessionId.value sessionId)

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.RegisterSource(sessionId: SessionId, source: SessionSignalSource) =
        let key = SessionId.value sessionId
        ownedSessions.Add key |> ignore
        sources.[key] <- source

    member _.UnregisterOwned(sessionId: SessionId) =
        let key = SessionId.value sessionId
        ownedSessions.Remove key |> ignore
        sources.Remove key |> ignore

    member private _.Forward(sourceToDrop: SessionSignalSource, signal: HostSignal) =
        let sessionId = HostSignalAdapter.sessionIdOf signal

        match sources.TryGetValue(SessionId.value sessionId) with
        | true, source when source = sourceToDrop -> ()
        | _ -> onSignal signal

    /// LOOP-009: text deltas first; coarse signals second; everything else drop.
    member private _.ObserveRaw(sourceToDrop: SessionSignalSource, raw: obj) =
        match onLoopEvent with
        | Some observe when LoopEventCodec.isLoopTextDelta raw -> observe raw
        | _ ->
            match HostSignalAdapter.tryAdapt isOwned raw with
            | Some signal -> this.Forward(sourceToDrop, signal)
            | None -> ()

    /// Plugin-local event hook path drops sessions registered as global-only.
    member _.ObserveLocal(raw: obj) =
        this.ObserveRaw(SessionSignalSource.GlobalForeignDirectoryEvent, raw)

    /// Global SSE path drops sessions registered as local-only.
    member _.ObserveGlobal(raw: obj) =
        this.ObserveRaw(SessionSignalSource.LocalPluginEvent, raw)

    member _.Observe(raw: obj) = this.ObserveLocal raw
