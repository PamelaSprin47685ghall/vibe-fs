module Wanxiangshu.Hosts.Omp.Fallback.EventTranslatorHelpers

/// Private helper functions for the OMP fallback event translator.

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.PartTypeClassify
open Wanxiangshu.Runtime.Fallback.FallbackEventBridge
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver

let ompErrorInput (err: obj) : ErrorInput =
    let getOpt key =
        let s = Dyn.str err key in if s <> "" then Some s else None

    { ErrorName = Dyn.str err "name"
      DomainError = Some(translateJsError err)
      Message = Dyn.str err "message"
      StatusCode = getOpt "statusCode" |> Option.map int
      IsRetryable = getOpt "isRetryable" |> Option.map ((=) "true") }

let private zwsChar = "\u200b"

let private getPartsTextLocal (parts: obj) : string =
    if not (Dyn.isArray parts) then
        ""
    else
        (parts :?> obj array)
        |> Array.choose (fun p ->
            if Dyn.str p "type" = "text" && not (Dyn.isNullish (Dyn.get p "text")) then
                Some(string (Dyn.get p "text"))
            else
                None)
        |> String.concat "\n"

let private isSyntheticText (text: string) : bool =
    let t = text.Trim()

    t = "\u200b"
    || [| "There are still incomplete todos"
          "You are in loop mode. You must call the submit_review"
          "A background runner task is still active"
          "the system context is about to be suspended"
          "You must immediately force an emergency stop" |]
       |> Array.exists t.Contains

let findProperty (targets: obj array) (keys: string array) : string =
    targets
    |> Array.tryPick (fun target ->
        keys
        |> Array.tryPick (fun key ->
            let s = Dyn.str target key
            if s <> "" then Some s else None))
    |> Option.defaultValue ""

let tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let getOpt target =
        if Dyn.isNullish target then rawEvent else target

    let ev = getOpt (Dyn.get rawEvent "event")
    let info = getOpt (Dyn.get ev "info")
    let props = getOpt (Dyn.get rawEvent "props")
    let targets = [| info; ev; props; rawEvent |]
    let keys = [| "turnId"; "turnID"; "runId"; "runID" |]
    let tid = findProperty targets keys
    if tid <> "" then Some(TurnId.create tid) else None

let tryGetModelStringFromInfo (info: obj) : string option =
    if Dyn.isNullish info then
        None
    else
        let mv = Dyn.get info "model"

        if Dyn.isNullish mv then
            None
        elif Dyn.typeIs mv "string" then
            let s = string mv in if s = "" then None else Some s
        else
            let pID, mID, variant =
                Dyn.str mv "providerID", Dyn.str mv "modelID", Dyn.str mv "variant"

            let suffix = if variant <> "" then ":" + variant else ""

            if pID = "" || mID = "" then
                let idVal = Dyn.str mv "id" in if idVal <> "" then Some(idVal + suffix) else None
            else
                Some(sprintf "%s/%s%s" pID mID suffix)

let ompIsNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
    let eventObj = Dyn.get rawEvent "event"

    if
        Dyn.str eventObj "type" <> "message.updated"
        || Dyn.str (Dyn.get eventObj "info") "role" <> "user"
    then
        false
    else
        let text = getPartsTextLocal (Dyn.get eventObj "parts")

        if isSyntheticText text then
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

            not (runtime.IsInjectedSince(sessionID, msgTime))

let private getAssistantEvidence (t: string) (text: string) (hasToolCall: bool) =
    let finish = Some(if hasToolCall then ToolFinish else NormalFinish)

    if t.StartsWith("message.part.") then
        AssistantDelta("", 0L, text, finish)
    else
        AssistantSnapshot("", 0L, text, finish)

let private checkToolCall (finishVal: string) (parts: obj) =
    let isToolFinish =
        FinishReason.isToolFinish (FinishReason.fromString finishVal)
        || (let lower = finishVal.ToLowerInvariant() in lower.Contains("tool") && lower <> "tool_use_error")

    let hasToolCallPart =
        if Dyn.isArray parts then
            (parts :?> obj array)
            |> Array.exists (fun part -> isToolCallPartType (Dyn.str part "type"))
        else
            false

    isToolFinish || hasToolCallPart

let private getRecovery (t: string) (rawEvent: obj) =
    if t = "message.updated" then
        match scanToolCallAsText [| rawEvent |] with
        | Some prompt -> RawToolCallDetected prompt
        | None -> NoRecoveryPrompt
    else
        NoRecoveryPrompt

let private handleAssistantRole (t: string) (info: obj) (rawEvent: obj) =
    let parts = Dyn.get info "parts"
    let text = getPartsTextLocal parts
    let finishVal = Dyn.str info "finish"
    let hasToolCall = checkToolCall finishVal parts
    let assistantEvidence = getAssistantEvidence t text hasToolCall
    let recovery = getRecovery t rawEvent

    Some
        { TurnId = tryExtractTurnIdFromEvent rawEvent
          Evidence =
            { CurrentTurnEvidence.empty with
                Assistant = assistantEvidence
                Recovery = recovery } }

let extractTurnObsFromMessage (t: string) (info: obj) (rawEvent: obj) =
    if Dyn.isNullish info then
        None
    else
        let role = Dyn.str info "role"

        if role = "assistant" then
            handleAssistantRole t info rawEvent
        elif role = "toolResult" then
            Some
                { TurnId = tryExtractTurnIdFromEvent rawEvent
                  Evidence =
                    { CurrentTurnEvidence.empty with
                        Tool = HasToolResult } }
        else
            None
