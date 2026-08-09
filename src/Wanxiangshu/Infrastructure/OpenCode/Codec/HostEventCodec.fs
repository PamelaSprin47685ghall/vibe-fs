namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// The only module that unwraps raw host `obj` events.
/// Outputs typed `HostSignal` for the coarse signals:
///   - session.status idle
///   - session.status retry
///   - session.deleted
///   - session.error as a non-durable failure wakeup
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
        elif not (isNull rawInput?data) && not (isNull rawInput?data?``type``) then
            rawInput?data
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

    let isHostSignalEvent (raw: obj) : bool =
        match eventTypeOf raw with
        | "session.status"
        | "session.deleted"
        | "session.error" -> true
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

            // ARCH-002: the event's `messageID` is deliberately not read. It was
            // taken as the failed assistant message and written into the fallback
            // cursor, which derives a domain fact from an event field. The failed
            // provider run comes from the reconciled snapshot (HOST-004).
            Some
                { SessionId = sessionId
                  Attempt = attempt
                  Reason = reason }

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
            | "session.deleted" ->
                match tryReadSessionId raw with
                | Some sessionId -> Some(SessionDeleted sessionId)
                | _ -> None
            | "session.error" ->
                match tryReadSessionId raw with
                | Some sessionId ->
                    let properties = raw?properties
                    let error = if isNull properties then null else properties?error

                    let name =
                        if isNull error || isNull error?name then
                            ""
                        else
                            unbox<string> error?name

                    if name = "MessageAbortedError" || name = "AbortError" then
                        // HOST-002/004: operator abort is a typed signal, not a
                        // dropped event. It revokes the attempt's idle-derived
                        // continuation capability; it must never be mistaken for
                        // ProviderFailure (which would wrongly advance fallback).
                        Some(AttemptAborted sessionId)
                    else
                        let reason =
                            if not (isNull error) && not (isNull error?message) then
                                unbox<string> error?message
                            elif not (isNull error) && not (isNull error?data) && not (isNull error?data?message) then
                                unbox<string> error?data?message
                            else
                                "provider failure"

                        Some(ProviderFailure(sessionId, reason))
                | _ -> None
            | _ -> None
