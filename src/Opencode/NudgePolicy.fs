module VibeFs.Opencode.NudgePolicy

open System
open VibeFs.Kernel
open VibeFs.Kernel.DomainError
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeEvents

// ── Dyn-boundary utility functions (stateless, no side effects) ──

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if eventType = "session.created" || eventType = "session.updated" || eventType = "session.deleted" then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

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

let isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false
