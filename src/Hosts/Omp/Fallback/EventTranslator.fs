module Wanxiangshu.Hosts.Omp.Fallback.EventTranslator

open Fable.Core
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Omp.Fallback.MessageInspection

type OmpEventTranslatorClass(runtime: FallbackRuntimeStore) =
    interface IEventTranslator with
        member _.TranslateError(rawEvent: obj) : FallbackEvent option =
            let ev = Dyn.get rawEvent "event"
            let t = Dyn.str ev "type"

            if t = "session.error" then
                let err = Dyn.get ev "error"

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
            let t = Dyn.str (Dyn.get rawEvent "event") "type"
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
                let id = Dyn.str info "id"
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

            let cid = findFirstStringValue targets [| "continuationId"; "continuationID" |]

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
            tryFindFirstStringValue targets [| "turnId"; "turnID"; "runId"; "runID" |]

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
                        { TurnId =
                            tryFindTurnId [| Dyn.get eventObj "info"; eventObj; Dyn.get rawEvent "props"; rawEvent |]
                          Evidence =
                            { CurrentTurnEvidence.empty with
                                Tool = HasToolResult } }
                else
                    None

let ompEventTranslator (runtime: FallbackRuntimeStore) : IEventTranslator = OmpEventTranslatorClass(runtime)
