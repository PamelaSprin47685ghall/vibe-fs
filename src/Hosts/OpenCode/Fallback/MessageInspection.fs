module Wanxiangshu.Hosts.Opencode.Fallback.MessageInspection
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Hosts.Opencode.Fallback.MessageInspectionObservation
open Wanxiangshu.Hosts.Opencode.Fallback.MessageInspectionIdentity

let private isSyntheticText (text: string) : bool =
    let t = text.Trim()

    t = "\u200b"
    || t.Contains("There are still incomplete todos")
    || t.Contains("command: with-review")
    || t.Contains("You are in loop mode. You must call the submit_review")
    || t.Contains("A background runner task is still active")
    || t.Contains("the system context is about to be suspended")
    || t.Contains("You must immediately force an emergency stop")

let internal isNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
    let props = getProps rawEvent
    let parts = Dyn.get props "parts"

    if Dyn.isNullish parts then
        false
    else
        let partsArr = parts :?> obj array
        let text = getPartsText parts

        let hasSyntheticMarker =
            partsArr
            |> Array.exists (fun part ->
                let synthetic = Dyn.get part "synthetic"
                not (Dyn.isNullish synthetic) && unbox<bool> synthetic)

        not hasSyntheticMarker && not (isSyntheticText text)

let internal translateErrorImpl (rawEvent: obj) : FallbackEvent option =
    let eventType = getEventType rawEvent

    if eventType = "session.error" then
        let errorObj = Dyn.get (getProps rawEvent) "error"

        if Dyn.isNullish errorObj then
            None
        else
            Some(FallbackEvent.SessionError(opencodeErrorInput errorObj))
    elif eventType = "session.interrupted" then
        Some(
            FallbackEvent.SessionError
                { ErrorName = "MessageAbortedError"
                  DomainError = Some MessageAborted
                  Message = "interrupted"
                  StatusCode = None
                  IsRetryable = Some false }
        )
    elif eventType = "session.status" then
        let statusObj = Dyn.get (getProps rawEvent) "status"
        let status = resolveStatusValue statusObj

        if status = "interrupted" || status = "abort" then
            Some(
                FallbackEvent.SessionError
                    { ErrorName = "MessageAbortedError"
                      DomainError = Some MessageAborted
                      Message = "interrupted"
                      StatusCode = None
                      IsRetryable = Some false }
            )
        else
            None
    else
        None

let internal extractTurnObservationImpl = MessageInspectionObservation.extractTurnObservationImpl
let internal isAssistantMessageImpl = MessageInspectionObservation.isAssistantMessageImpl
let internal extractAssistantMessageIdImpl = MessageInspectionIdentity.extractAssistantMessageIdImpl
let internal extractAssistantParentIdImpl = MessageInspectionIdentity.extractAssistantParentIdImpl
let internal extractContinuationIdentityImpl = MessageInspectionIdentity.extractContinuationIdentityImpl
let internal extractHostRunIdImpl = MessageInspectionIdentity.extractHostRunIdImpl
