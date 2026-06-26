module Wanxiangshu.Shell.OpencodeSessionEventCodecCommon

open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Shell.Dyn

/// Session lifecycle event types that carry `id` on `info` rather than `sessionID`.
let sessionEventTypes =
    Set.ofList [
        "session.created"
        "session.updated"
        "session.deleted"
        "session.delete"
        "session.close"
        "session.remove"
    ]

/// Resolve the sessionID of a session event. Different event shapes stash the
/// id in different locations — fall back through the known carriers in
/// priority order, and only fall through to `info.id` for the lifecycle-style
/// events that carry the id directly on `info`.
let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if Set.contains eventType sessionEventTypes then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

/// Concatenate text content from an Opencode `parts` array. Non-array or
/// non-text parts are silently skipped so callers can pass either a typed
/// list or a missing payload without a separate guard.
let getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

/// Test whether `info` represents a completed assistant message. An assistant
/// message counts as completed when its role is assistant AND it carries a
/// terminal finish OR a numeric `time.completed`. An error-bearing message is
/// never considered completed (aborts are surfaced separately).
let isCompletedAssistantMessage (info: obj) : bool =
    if Dyn.isNullish info then false
    else
        let isAssistant = Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"
        let hasError = not (Dyn.isNullish (Dyn.get info "error"))
        if not isAssistant || hasError then false
        else
            let finishVal = Dyn.get info "finish"
            if not (Dyn.isNullish finishVal) && Dyn.typeIs finishVal "string" then
                isTerminalAssistantFinish (string finishVal)
            else
                let timeCompleted = Dyn.get (Dyn.get info "time") "completed"
                not (Dyn.isNullish timeCompleted) && Dyn.typeIs timeCompleted "number"
