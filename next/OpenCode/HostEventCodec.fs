namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// The only module that unwraps raw host `obj` events.
/// Outputs typed `HostSignal` for the coarse signals:
///   - session.status idle
///   - session.status retry
///   - session.error
///   - session.deleted
/// All other raw payloads return None.
module HostEventCodec =

    let unwrap (rawInput: obj) : obj =
        if isNull rawInput then
            rawInput
        elif not (isNull rawInput?event) then
            rawInput?event
        elif not (isNull rawInput?payload) then
            let payload = rawInput?payload

            if not (isNull rawInput?directory) && isNull payload?directory then
                payload?directory <- rawInput?directory

            payload
        else
            rawInput

    let eventTypeOf (raw: obj) : string =
        if isNull raw || isNull raw?``type`` then
            ""
        else
            (unbox<string> raw?``type``).ToLowerInvariant()

    let private tryReadSessionId (raw: obj) : SessionId option =
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

    let trySessionId (raw: obj) = tryReadSessionId raw

    let private tryReadMessageId (raw: obj) : MessageId option =
        let properties = if isNull raw then null else raw?properties

        if not (isNull properties) && not (isNull properties?messageID) then
            Some(MessageId.create (unbox<string> properties?messageID))
        elif not (isNull properties) && not (isNull properties?messageId) then
            Some(MessageId.create (unbox<string> properties?messageId))
        else
            None

    let isAbortedError (errorObj: obj) : bool =
        if isNull errorObj then
            false
        else
            let name = errorObj?name

            not (isNull name)
            && (unbox<string> name = "MessageAbortedError" || unbox<string> name = "AbortError")

    let isHostSignalEvent (raw: obj) : bool =
        match eventTypeOf raw with
        | "session.status"
        | "session.error"
        | "session.deleted" -> true
        | _ -> false

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

            Some
                { SessionId = sessionId
                  Attempt = attempt
                  Reason = reason
                  MessageId = tryReadMessageId raw }

    let private providerErrorSignal (sessionId: SessionId) (raw: obj) : ProviderErrorSignal option =
        let properties = if isNull raw then null else raw?properties
        let error = if isNull properties then null else properties?error

        if isNull error || isAbortedError error then
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

                Some
                    { SessionId = sessionId
                      Reason = reason
                      StatusCode = statusCode
                      IsRetryable = isRetryable
                      MessageId = tryReadMessageId raw }

    let tryDecode (rawInput: obj) : HostSignal option =
        let raw = unwrap rawInput

        if isNull raw then
            None
        else
            match eventTypeOf raw with
            | "session.status" ->
                match tryReadSessionId raw with
                | None -> None
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
                match tryReadSessionId raw with
                | None -> None
                | Some sessionId -> providerErrorSignal sessionId raw |> Option.map ProviderError
            | "session.deleted" ->
                match tryReadSessionId raw with
                | Some sessionId -> Some(SessionDeleted sessionId)
                | _ -> None
            | _ -> None
