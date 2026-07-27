namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop

/// Tracks the most-recent assistant message per session so a terminal event can
/// consume it exactly once. Also tracks the last consumed message ID so
/// duplicate/replayed `message.updated` events are rejected at Record time.
/// Only an assistant role is recorded and only a terminal event consumes —
/// provider-retry merely peeks the current message id.
type AssistantTurnTracker() =
    let lastAssistantMsgs = Dictionary<string, obj>()
    let lastConsumedIds = Dictionary<string, string>()
    let lastUserMsgIds = Dictionary<string, string>()

    let getMsgId (msg: obj) : string option =
        if isNull msg then None
        else
            let info = msg?info
            if not (isNull info) && not (isNull info?id) then
                Some(unbox<string> info?id)
            elif not (isNull msg?id) then
                Some(unbox<string> msg?id)
            else
                None

    /// Records the current assistant message for a session, always replacing
    /// any previous one. Consumed-ID tracking is only enforced at consumption
    /// time (TakeTerminal) — recording must never skip a message because a
    /// stale assistant or cross-turn blogger/coder messages have different IDs.
    member _.Record(sessionId: string, message: obj) =
        lastAssistantMsgs.[sessionId] <- message

    /// Clears the current unconsumed assistant for a session. Called on abort
    /// so a torn-down assistant cannot be consumed by a later terminal.
    member _.ClearCurrent(sessionId: string) =
        lastAssistantMsgs.Remove sessionId |> ignore

    /// A user message starts a new turn only when its id differs from the last
    /// user id observed for this session. OpenCode re-emits the same user
    /// `message.updated` while the assistant is in flight (before provider
    /// retry); those replays must NOT clear the assistant id, or
    /// `session.status=retry` loses the sole stable fallback identity (SSOT §6).
    member _.NoteUser(sessionId: string, userMessage: obj) =
        match getMsgId userMessage with
        | Some userId ->
            match lastUserMsgIds.TryGetValue sessionId with
            | true, previous when previous = userId -> ()
            | _ ->
                lastUserMsgIds.[sessionId] <- userId
                lastAssistantMsgs.Remove sessionId |> ignore
        | None ->
            lastAssistantMsgs.Remove sessionId |> ignore

    /// Read-only peek of the current assistant message id, or "" when no message
    /// is pending. Used by the provider-retry attributor (SSOT §6) and never
    /// removes the message — a terminal is the only consumer.
    member _.LastMessageId(sessionId: string) : string =
        match lastAssistantMsgs.TryGetValue sessionId with
        | true, lastMsg -> FallbackDetect.messageId sessionId lastMsg
        | false, _ -> ""

    /// Consumes the current assistant message exactly once: a terminal takes and
    /// removes it, then records the message ID as consumed so a duplicate
    /// `message.updated` (followed by duplicate `session.idle`) cannot re-consume
    /// the same message a second time.
    member _.TakeTerminal(sessionId: string) : obj option =
        match lastAssistantMsgs.TryGetValue sessionId with
        | true, lastMsg ->
            let msgId = getMsgId lastMsg

            // If this message ID was already consumed by a prior terminal
            // (duplicate event replay), return None — no side effects.
            match msgId, lastConsumedIds.TryGetValue sessionId with
            | Some id, (true, consumedId) when consumedId = id -> None
            | _ ->
                lastAssistantMsgs.Remove sessionId |> ignore
                msgId |> Option.iter (fun id -> lastConsumedIds.[sessionId] <- id)
                Some lastMsg
        | false, _ -> None

/// Companion module: terminal message id / model derivation from a message
/// consumed via `AssistantTurnTracker.TakeTerminal`.
module AssistantTurnTracker =

    /// Terminal message id derivation for a consumed assistant: the hydrated
    /// assistant id, or the literal "terminal" sentinel when no assistant
    /// message was consumed (in which case it is never used by a
    /// guard/continuation path).
    let terminalMessageId (sessionId: string) (takenAssistant: obj option) : string =
        match takenAssistant with
        | Some lastMsg -> FallbackDetect.messageId sessionId lastMsg
        | None -> "terminal"

    /// Terminal model derivation from a consumed assistant message: prefers the
    /// top-level providerID/modelID pair, else the nested info.model pair. None
    /// when the message carries no model identity.
    let terminalModel (takenAssistant: obj option) : OpencodeModel option =
        match takenAssistant with
        | Some lastMsg when not (isNull lastMsg?info) ->
            let info = lastMsg?info

            if not (isNull info?providerID) && not (isNull info?modelID) then
                Some
                    { providerID = unbox<string> info?providerID
                      modelID = unbox<string> info?modelID
                      variant = None }
            elif not (isNull info?model) && not (isNull info?model?providerID) then
                Some
                    { providerID = unbox<string> info?model?providerID
                      modelID = unbox<string> info?model?modelID
                      variant = None }
            else
                None
        | _ -> None
