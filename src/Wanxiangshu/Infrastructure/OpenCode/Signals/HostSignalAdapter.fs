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

    /// HOST-027 reasoning deltas and LOOP-009 visible text are disjoint sensor
    /// streams. NeedHelp gets first refusal because LoopEventCodec's coarse
    /// message.part.delta predicate intentionally includes every field before its
    /// own decoder filters to text.
    member _.Observe(raw: obj) =
        match onNeedHelpEvent with
        | Some observe when NeedHelpEventCodec.isNeedHelpDelta raw -> observe raw
        | _ ->
            match onLoopEvent with
            | Some observe when LoopEventCodec.isLoopTextDelta raw -> observe raw
            | _ ->
                match HostSignalAdapter.tryAdapt isOwned raw with
                | Some signal -> onSignal signal
                | None -> ()

    member this.ObserveLocal(raw: obj) = this.Observe raw
