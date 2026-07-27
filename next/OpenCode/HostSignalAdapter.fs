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

        if isNull status || isNull status?``type`` || unbox<string> status?``type`` <> "retry" then
            None
        else
            let attempt =
                if isNull status?attempt then "unknown" else string status?attempt

            let reason =
                if isNull status?message then "provider retry" else unbox<string> status?message

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
            | "session.idle" ->
                match sessionIdOf raw with
                | Some sessionId when isOwned sessionId -> Some(SessionIdle sessionId)
                | _ -> None
            | "session.deleted" ->
                match sessionIdOf raw with
                | Some sessionId when isOwned sessionId -> Some(SessionDeleted sessionId)
                | _ -> None
            | "session.error" ->
                match sessionIdOf raw with
                | None -> None
                | Some sessionId when not (isOwned sessionId) -> None
                | Some sessionId ->
                    let properties = raw?properties
                    let error = if isNull properties then null else properties?error
                    let name =
                        if isNull error || isNull error?name then ""
                        else unbox<string> error?name
                    if name.IndexOf("Abort", StringComparison.OrdinalIgnoreCase) >= 0 then
                        Some(SessionAbort sessionId)
                    else
                        None
            | _ -> None

type HostSignalRouter(ownedSessions: HashSet<string>, onSignal: HostSignal -> unit) as this =
    let sources = Dictionary<string, SessionSignalSource>()

    // Empty registry means "accept all" only before the first owned session is
    // registered; after that only explicit owned sessions pass.
    let isOwned (sessionId: SessionId) =
        ownedSessions.Count = 0
        || ownedSessions.Contains(SessionId.value sessionId)

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
    member _.ObserveLocal(raw: obj) =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | None -> ()
        | Some signal ->
            let sid =
                match signal with
                | SessionIdle id
                | SessionDeleted id
                | SessionAbort id -> id
                | ProviderRetry retry -> retry.SessionId

            match sources.TryGetValue(SessionId.value sid) with
            | true, GlobalForeignDirectoryEvent -> ()
            | _ -> onSignal signal

    /// Global SSE path. Drops sessions registered as local-only; unregistered
    /// owned sessions still pass (legacy accept until source is set).
    member _.ObserveGlobal(raw: obj) =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | None -> ()
        | Some signal ->
            let sid =
                match signal with
                | SessionIdle id
                | SessionDeleted id
                | SessionAbort id -> id
                | ProviderRetry retry -> retry.SessionId

            match sources.TryGetValue(SessionId.value sid) with
            | true, LocalPluginEvent -> ()
            | _ -> onSignal signal

    member _.Observe(raw: obj) = this.ObserveLocal raw
