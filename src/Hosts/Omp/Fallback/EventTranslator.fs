module Wanxiangshu.Hosts.Omp.Fallback.EventTranslator

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

            not (runtime.IsInjectedSince(sessionID, msgTime))

let private findProperty (targets: obj array) (keys: string array) : string =
    targets
    |> Array.tryPick (fun target ->
        keys
        |> Array.tryPick (fun key -> let s = Dyn.str target key in if s <> "" then Some s else None))
    |> Option.defaultValue ""

let private tryExtractTurnIdFromEvent (rawEvent: obj) : TurnId option =
    let getOpt target =
        if Dyn.isNullish target then rawEvent else target

    let ev = getOpt (Dyn.get rawEvent "event")
    let info = getOpt (Dyn.get ev "info")
    let props = getOpt (Dyn.get rawEvent "props")
    let targets = [| info; ev; props; rawEvent |]
    let keys = [| "turnId"; "turnID"; "runId"; "runID" |]
    let tid = findProperty targets keys
    if tid <> "" then Some(TurnId.create tid) else None

let private tryGetModelStringFromInfo (info: obj) : string option =
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

let private extractTurnObsFromMessage (t: string) (info: obj) (rawEvent: obj) =
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
                        let pt = Dyn.str p "type" in

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

type OmpEventTranslatorClass(runtime: FallbackRuntimeStore) =
    interface IEventTranslator with
        member _.TranslateError(rawEvent: obj) : FallbackEvent option =
            let ev = Dyn.get rawEvent "event"
            let t = Dyn.str ev "type"

            if t = "session.error" then
                let err = Dyn.get ev "error" in

                if Dyn.isNullish err then
                    None
                else
                    Some(SessionError(ompErrorInput err))
            elif t = "session.abort" || t = "session.interrupted" then
                Some(
                    SessionError
                        { ErrorName = "MessageAbortedError"
                          DomainError = Some MessageAborted
                          Message = "aborted"
                          StatusCode = None
                          IsRetryable = Some false }
                )
            else
                None

        member _.ExtractSessionID(rawEvent: obj) =
            Dyn.str (Dyn.get rawEvent "props") "sessionID"

        member _.IsSessionError(rawEvent: obj) =
            let t = Dyn.str (Dyn.get rawEvent "event") "type" in
            t = "session.error" || t = "session.abort" || t = "session.interrupted"

        member _.IsSessionIdle(rawEvent: obj) =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.idle"

        member _.IsSessionBusy(rawEvent: obj) =
            Dyn.str (Dyn.get rawEvent "event") "type" = "session.busy"

        member _.IsNewUserMessage(sessionID, rawEvent) =
            ompIsNewUserMessageImpl runtime sessionID rawEvent

        member _.ExtractNewUserMessageId(rawEvent) =
            let id = Dyn.str (Dyn.get (Dyn.get rawEvent "event") "info") "id"
            if id = "" then None else Some id

        member _.ExtractRoutingContext(rawEvent) =
            let info = Dyn.get (Dyn.get rawEvent "event") "info"
            let agent = Dyn.get info "agent"
            tryGetModelStringFromInfo info, (if Dyn.isNullish agent then None else Some(string agent))

        member _.IsAssistantMessage(rawEvent: obj) =
            let ev = Dyn.get rawEvent "event"

            if Dyn.isNullish ev then
                false
            else
                let t, info = Dyn.str ev "type", Dyn.get ev "info"

                (t = "message.updated" || t.StartsWith("message.part."))
                && not (Dyn.isNullish info)
                && Dyn.str info "role" = "assistant"

        member _.ExtractAssistantMessageId(rawEvent: obj) =
            let ev = Dyn.get rawEvent "event"

            if
                Dyn.isNullish ev
                || Dyn.str ev "type" = "session.idle"
                || Dyn.str ev "type" = "session.error"
            then
                None
            else
                let info = Dyn.get ev "info"
                let info = if Dyn.isNullish info then ev else info
                let id = Dyn.str info "id" in
                if id <> "" then Some id else None

        member _.ExtractAssistantParentId(rawEvent: obj) =
            let ev = Dyn.get rawEvent "event"

            if Dyn.isNullish ev then
                None
            else
                let info = Dyn.get ev "info"
                let info = if Dyn.isNullish info then ev else info
                let pid = Dyn.str info "parentID"
                let pid = if pid <> "" then pid else Dyn.str info "parentId"
                if pid <> "" then Some pid else None

        member _.ExtractContinuationIdentity(rawEvent: obj) =
            let getOpt target =
                if Dyn.isNullish target then rawEvent else target

            let ev = getOpt (Dyn.get rawEvent "event")
            let info = getOpt (Dyn.get ev "info")
            let props = getOpt (Dyn.get rawEvent "props")
            let targets = [| info; ev; props; rawEvent |]
            let cid = findProperty targets [| "continuationId"; "continuationID" |]

            let findOrdinal () =
                targets
                |> Array.tryPick (fun target ->
                    let o = Dyn.get target "continuationOrdinal"
                    if not (Dyn.isNullish o) then Some o else None)
                |> Option.defaultValue (unbox null)

            let ord = getOrdinal (findOrdinal ())
            if cid <> "" then Some(cid, ord) else None

        member _.ExtractHostRunId(rawEvent: obj) =
            let getOpt target =
                if Dyn.isNullish target then rawEvent else target

            let ev = getOpt (Dyn.get rawEvent "event")
            let info = getOpt (Dyn.get ev "info")
            let props = getOpt (Dyn.get rawEvent "props")
            let targets = [| info; ev; props; rawEvent |]
            let keys = [| "turnId"; "turnID"; "runId"; "runID" |]
            let tid = findProperty targets keys
            if tid <> "" then Some tid else None

        member _.ExtractTurnObservation(rawEvent: obj) : TurnObservation option =
            let eventObj = Dyn.get rawEvent "event"

            if Dyn.isNullish eventObj then
                None
            else
                let t = Dyn.str eventObj "type"

                if t = "message.updated" || t.StartsWith("message.part.") then
                    extractTurnObsFromMessage t (Dyn.get eventObj "info") rawEvent
                elif t = "tool_result" then
                    Some
                        { TurnId = tryExtractTurnIdFromEvent rawEvent
                          Evidence =
                            { CurrentTurnEvidence.empty with
                                Tool = HasToolResult } }
                else
                    None

let ompEventTranslator (runtime: FallbackRuntimeStore) : IEventTranslator = OmpEventTranslatorClass(runtime)
