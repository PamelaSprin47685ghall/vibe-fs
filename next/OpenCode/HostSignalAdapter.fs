namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Thin JS/Fable boundary filter. No normalize recursion, no part parsing,
/// no journal, no business effects — only idle/retry/deleted signals.
module HostSignalAdapter =

    let private asString (value: obj) =
        if isNull value then
            None
        else
            try
                let text = string value
                if String.IsNullOrWhiteSpace text then None else Some text
            with _ ->
                None

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

            let candidates =
                [ if not (isNull properties) && not (isNull properties?sessionID) then
                      Some(unbox<string> properties?sessionID)
                  else
                      None
                  if not (isNull properties) && not (isNull properties?sessionId) then
                      Some(unbox<string> properties?sessionId)
                  else
                      None
                  if not (isNull raw?sessionID) then Some(unbox<string> raw?sessionID) else None
                  if not (isNull raw?sessionId) then Some(unbox<string> raw?sessionId) else None ]

            candidates
            |> List.tryPick id
            |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))
            |> Option.map SessionId.create

    let private retrySignal (sessionId: SessionId) (raw: obj) : RetrySignal option =
        let properties = if isNull raw then null else raw?properties
        let status = if isNull properties then null else properties?status

        if isNull status || isNull status?``type`` || unbox<string> status?``type`` <> "retry" then
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

    /// Returns Some signal only for owned idle/retry/deleted. Everything else
    /// is dropped at the earliest boundary with a single string compare.
    let tryAdapt (isOwned: SessionId -> bool) (raw: obj) : HostSignal option =
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
            | _ -> None

type HostSignalRouter(ownedSessions: HashSet<string>, onSignal: HostSignal -> unit) =

    let isOwned (sessionId: SessionId) =
        ownedSessions.Count = 0
        || ownedSessions.Contains(SessionId.value sessionId)

    member _.RegisterOwned(sessionId: SessionId) =
        ownedSessions.Add(SessionId.value sessionId) |> ignore

    member _.UnregisterOwned(sessionId: SessionId) =
        ownedSessions.Remove(SessionId.value sessionId) |> ignore

    member _.Observe(raw: obj) =
        match HostSignalAdapter.tryAdapt isOwned raw with
        | None -> ()
        | Some signal -> onSignal signal
