namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Fable.Core.JsInterop

/// Tracks the most-recent assistant message per session so a terminal event can
/// consume it exactly once. Only an assistant role is recorded and only a
/// terminal event consumes — provider-retry merely peeks the current message id.
type AssistantTurnTracker() =
    let lastAssistantMsgs = Dictionary<string, obj>()

    /// Records the current assistant message for a session, replacing any
    /// previous one. Only the resolved assistant role is recorded by the caller.
    member _.Record(sessionId: string, message: obj) =
        lastAssistantMsgs.[sessionId] <- message

    /// Read-only peek of the current assistant message id, or "" when no message
    /// is pending. Used by the provider-retry attributor (SSOT §6) and never
    /// removes the message — a terminal is the only consumer.
    member _.LastMessageId(sessionId: string) : string =
        match lastAssistantMsgs.TryGetValue sessionId with
        | true, lastMsg -> FallbackDetect.messageId sessionId lastMsg
        | false, _ -> ""

    /// Consumes the current assistant message exactly once: a terminal takes and
    /// removes it. A duplicate or stray idle (restart/shutdown re-propagation)
    /// finds no message and returns None, performing NO message-level side
    /// effects — no fallback fact, no guard nudge, no continuation.
    member _.TakeTerminal(sessionId: string) : obj option =
        match lastAssistantMsgs.TryGetValue sessionId with
        | true, lastMsg ->
            lastAssistantMsgs.Remove sessionId |> ignore
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
