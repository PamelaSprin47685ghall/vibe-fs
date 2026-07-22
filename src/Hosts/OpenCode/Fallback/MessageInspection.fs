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
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

let internal isNewUserMessageImpl (runtime: FallbackRuntimeStore) (sessionID: string) (rawEvent: obj) : bool =
    let props = getProps rawEvent
    let parts = Dyn.get props "parts"

    if Dyn.isNullish parts then
        false
    else
        let partsArr = parts :?> obj array

        let hasSyntheticMarker =
            partsArr
            |> Array.exists (fun part ->
                let synthetic = Dyn.get part "synthetic"
                not (Dyn.isNullish synthetic) && unbox<bool> synthetic)

        let metaRecord = WanxiangshuMetadataCodec.tryDecodeFromParts parts

        let isSyntheticOrigin =
            match metaRecord with
            | Some m ->
                match m.Origin with
                | Some orig -> MessageOrigin.isNudge orig || orig = MessageOrigin.FallbackContinuation
                | None ->
                    m.Kind = WanxiangshuMetadataCodec.nudgeKind
                    || m.Kind = WanxiangshuMetadataCodec.fallbackContinuationKind
            | None -> false

        let text = getPartsText parts
        let isZeroWidthSpace = text.Trim() = "\u200b"

        not hasSyntheticMarker && not isSyntheticOrigin && not isZeroWidthSpace

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

let internal extractTurnObservationImpl =
    MessageInspectionObservation.extractTurnObservationImpl

let internal isAssistantMessageImpl =
    MessageInspectionObservation.isAssistantMessageImpl

let internal extractAssistantMessageIdImpl =
    MessageInspectionIdentity.extractAssistantMessageIdImpl

let internal extractAssistantParentIdImpl =
    MessageInspectionIdentity.extractAssistantParentIdImpl

let internal extractContinuationIdentityImpl =
    MessageInspectionIdentity.extractContinuationIdentityImpl

let internal extractHostRunIdImpl = MessageInspectionIdentity.extractHostRunIdImpl
