namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Thin JS/Fable boundary filter. No part parsing, no journal, no business
/// effects — only idle/retry/deleted signals for owned sessions.
module HostSignalAdapter =

    let private unwrap (raw: obj) =
        if isNull raw then
            raw
        elif not (isNull raw?event) then
            raw?event
        elif not (isNull raw?payload) then
            let payload = raw?payload

            if not (isNull raw?directory) && isNull payload?directory then
                payload?directory <- raw?directory

            payload
        else
            raw

    let private eventTypeOf (raw: obj) =
        if isNull raw || isNull raw?``type`` then
            ""
        else
            (unbox<string> raw?``type``).ToLowerInvariant()

    let private sessionIdOf (raw: obj) =
        if isNull raw then
            None
        else
            let properties = raw?properties

            if not (isNull properties) && not (isNull properties?sessionID) then
                Some(SessionId.create (unbox<string> properties?sessionID))
            elif not (isNull properties) && not (isNull properties?sessionId) then
                Some(SessionId.create (unbox<string> properties?sessionId))
            elif not (isNull raw?sessionID) then
                Some(SessionId.create (unbox<string> raw?sessionID))
            elif not (isNull raw?sessionId) then
                Some(SessionId.create (unbox<string> raw?sessionId))
            else
                None

    let private retrySignal (sessionId: SessionId) (raw: obj) : RetrySignal option =
        let properties = if isNull raw then null else raw?properties
        let status = if isNull properties then null else properties?status

        if
            isNull status
            || isNull status?``type``
            || unbox<string> status?``type`` <> "retry"
        then
            None
        else
            let attempt =
                if isNull status?attempt then
                    "unknown"
                else
                    string status?attempt

            let reason =
                if isNull status?message then
                    "provider retry"
                else
                    unbox<string> status?message

            let messageId =
                if not (isNull properties) && not (isNull properties?messageID) then
                    Some(MessageId.create (unbox<string> properties?messageID))
                elif not (isNull properties) && not (isNull properties?messageId) then
                    Some(MessageId.create (unbox<string> properties?messageId))
                else
                    None

            Some
                { SessionId = sessionId
                  Attempt = attempt
                  Reason = reason
                  MessageId = messageId }

    /// Non-retryable provider failure with no assistant message.
    /// Host emits session.error then idle; idle alone cannot classify TurnFailed.
    let private providerErrorSignal (sessionId: SessionId) (raw: obj) : ProviderErrorSignal option =
        let properties = if isNull raw then null else raw?properties
        let error = if isNull properties then null else properties?error

        if isNull error || HostEventCodec.isAbortedError error then
            None
        else
            let data = if isNull error?data then null else error?data

            let statusCode =
                if isNull data || isNull data?statusCode then
                    None
                else
                    Some(unbox<int> data?statusCode)

            let isRetryable =
                if isNull data || isNull data?isRetryable then
                    None
                else
                    Some(unbox<bool> data?isRetryable)

            // Non-retryable provider failures (host is not retrying itself).
            // - explicit isRetryable=false (any status code, including 5xx)
            // - no retry hint: accept 4xx, or accept missing statusCode for raw
            //   provider stream errors like "Devin stream error invalid_argument".
            // User/host aborts are filtered out above.
            let accepted =
                match isRetryable with
                | Some true -> false
                | Some false -> true
                | None ->
                    match statusCode with
                    | Some code when code > 0 && code < 500 -> true
                    | None -> true
                    | _ -> false

            if not accepted then
                None
            else
                let reason =
                    if not (isNull data) && not (isNull data?message) then
                        unbox<string> data?message
                    elif not (isNull error?message) then
                        unbox<string> error?message
                    else
                        "provider error"

                let messageId =
                    if not (isNull properties) && not (isNull properties?messageID) then
                        Some(MessageId.create (unbox<string> properties?messageID))
                    elif not (isNull properties) && not (isNull properties?messageId) then
                        Some(MessageId.create (unbox<string> properties?messageId))
                    else
                        None

                Some
                    { SessionId = sessionId
                      Reason = reason
                      StatusCode = statusCode
                      IsRetryable = isRetryable
                      MessageId = messageId }

    /// SSOT signals: session.status idle|retry, non-retryable session.error,
    /// and session.deleted.
    let tryAdapt (isOwned: SessionId -> bool) (rawInput: obj) : HostSignal option =
        let raw = unwrap rawInput

        if isNull raw then
            None
        else
            match eventTypeOf raw with
            | "session.status" ->
                match sessionIdOf raw with
                | None -> None
                | Some sessionId when not (isOwned sessionId) -> None
                | Some sessionId ->
                    let properties = raw?properties
                    let status = if isNull properties then null else properties?status

                    if isNull status || isNull status?``type`` then
                        None
                    else
                        match unbox<string> status?``type`` with
                        | "idle" -> Some(SessionIdle sessionId)
                        | "retry" -> retrySignal sessionId raw |> Option.map ProviderRetry
                        | _ -> None
            | "session.error" ->
                match sessionIdOf raw with
                | None -> None
                | Some sessionId when not (isOwned sessionId) -> None
                | Some sessionId -> providerErrorSignal sessionId raw |> Option.map ProviderError
            | "session.deleted" ->
                match sessionIdOf raw with
                | Some sessionId when isOwned sessionId -> Some(SessionDeleted sessionId)
                | _ -> None
            | _ -> None

type HostSignalRouter(ownedSessions: HashSet<string>, onSignal: HostSignal -> unit) as this =
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

    /// Plugin-local event hook path. Drops sessions registered as global-only.
    /// ProviderError is an exception: host often emits it only on global SSE even
    /// for local sessions, so both paths accept it for owned sessions.
    member _.ObserveLocal(raw: obj) =
        let decoded = HostEventCodec.unwrap raw

        if HostEventCodec.eventTypeOf decoded = "session.error" then
            match HostEventCodec.trySessionId decoded with
            | Some sessionId when not (isOwned sessionId) ->
                // The plugin event hook is directory-scoped. Admit its local
                // provider error so full-snapshot authority reconciliation can
                // prove or reject the session before any retry side effect.
                this.RegisterOwned sessionId
                this.RegisterSource(sessionId, SessionSignalSource.LocalPluginEvent)
            | _ -> ()

        match HostSignalAdapter.tryAdapt isOwned raw with
        | None -> ()
        | Some(ProviderError _ as signal) -> onSignal signal
        | Some signal ->
            let sid =
                match signal with
                | SessionIdle id
                | SessionDeleted id -> id
                | ProviderRetry retry -> retry.SessionId
                | ProviderError err -> err.SessionId

            match sources.TryGetValue(SessionId.value sid) with
            | true, GlobalForeignDirectoryEvent -> ()
            | _ -> onSignal signal

    /// Global SSE path. Drops sessions registered as local-only, except
    /// ProviderError (see ObserveLocal).
    member _.ObserveGlobal(raw: obj) =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | None -> ()
        | Some(ProviderError _ as signal) -> onSignal signal
        | Some signal ->
            let sid =
                match signal with
                | SessionIdle id
                | SessionDeleted id -> id
                | ProviderRetry retry -> retry.SessionId
                | ProviderError err -> err.SessionId

            match sources.TryGetValue(SessionId.value sid) with
            | true, LocalPluginEvent -> ()
            | _ -> onSignal signal

    member _.Observe(raw: obj) = this.ObserveLocal raw
