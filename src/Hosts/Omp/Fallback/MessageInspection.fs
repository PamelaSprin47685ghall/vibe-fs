module Wanxiangshu.Hosts.Omp.Fallback.MessageInspection

open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.TypeClassify
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback
open Wanxiangshu.Runtime.Fallback.HostEventInspection
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

let ompErrorInput (err: obj) : ErrorInput =
    let getOpt key =
        let s = Dyn.str err key in if s <> "" then Some s else None

    { ErrorName = Dyn.str err "name"
      DomainError = Some(translateJsError err)
      Message = Dyn.str err "message"
      StatusCode = getOpt "statusCode" |> Option.map int
      IsRetryable = getOpt "isRetryable" |> Option.map ((=) "true") }

let ompIsNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
    let eventObj = Dyn.get rawEvent "event"

    if
        Dyn.str eventObj "type" <> "message.updated"
        || Dyn.str (Dyn.get eventObj "info") "role" <> "user"
    then
        false
    else
        let parts = Dyn.get eventObj "parts"

        let text =
            if Dyn.isArray parts then
                (parts :?> obj array)
                |> Array.choose (fun p ->
                    if Dyn.str p "type" = "text" then
                        Some(string p?text)
                    else
                        None)
                |> String.concat "\n"
            else
                ""

        if text = "" then
            false
        else
            let time = Dyn.get (Dyn.get eventObj "info") "time"

            let completed =
                if Dyn.isNullish time then
                    null
                else
                    Dyn.get time "completed"

            let msgTime =
                match completed with
                | :? int64 as i -> i
                | :? float as f -> int64 f
                | :? int as i32 -> int64 i32
                | _ -> 0L

            not (isInjectedSince msgTime (runtime.GetSession sessionID))

let private tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let getOpt target =
        if Dyn.isNullish target then rawEvent else target

    let ev = getOpt (Dyn.get rawEvent "event")
    let info = getOpt (Dyn.get ev "info")
    let props = getOpt (Dyn.get rawEvent "props")
    HostEventInspection.tryFindTurnId [| info; ev; props; rawEvent |]

let extractTurnObsFromMessage (t: string) (info: obj) (rawEvent: obj) : TurnObservation option =
    if Dyn.isNullish info then
        None
    else
        let role = Dyn.str info "role"

        if role = "assistant" then
            let parts = Dyn.get info "parts"

            let text =
                if Dyn.isArray parts then
                    (parts :?> obj array)
                    |> Array.choose (fun p ->
                        let pt = Dyn.str p "type"

                        if pt = "text" || pt = "signed" then
                            Some(string p?text)
                        else
                            None)
                    |> String.concat "\n"
                else
                    ""

            let finishVal = Dyn.str info "finish"

            let hasToolCall =
                FinishReason.isToolFinish (FinishReason.fromString finishVal)
                || (if Dyn.isArray parts then
                        (parts :?> obj array)
                        |> Array.exists (fun p -> isToolCallPartType (Dyn.str p "type"))
                    else
                        false)

            let f = Some(if hasToolCall then ToolFinish else NormalFinish)

            Some
                { TurnId = tryExtractTurnIdFromEvent rawEvent
                  Evidence =
                    { CurrentTurnEvidence.empty with
                        Assistant =
                            (if t.StartsWith("message.part.") then
                                 AssistantDelta("", 0L, text, f)
                             else
                                 AssistantSnapshot("", 0L, text, f))
                        Recovery = NoRecoveryPrompt } }
        elif role = "toolResult" then
            Some
                { TurnId = tryExtractTurnIdFromEvent rawEvent
                  Evidence =
                    { CurrentTurnEvidence.empty with
                        Tool = HasToolResult } }
        else
            None
