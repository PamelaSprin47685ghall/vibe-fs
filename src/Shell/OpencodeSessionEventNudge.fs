module VibeFs.Shell.OpencodeSessionEventNudge

open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.Nudge.Types
open VibeFs.Shell.Dyn
open VibeFs.Shell.ErrorClassify
open VibeFs.Shell.OpencodeSessionEventCodecCommon

/// Pure event type to NudgeHostEvent mappings — no payload inspection required.
let private pureMap =
    Map [
        "stream-abort", StreamAbort
        "session.delete", SessionDeleted
        "session.close", SessionDeleted
        "session.remove", SessionDeleted
        "session.deleted", SessionDeleted
        "session.next.retried", SessionNextRetried
        "session.idle", SessionIdle
    ]

/// Event extractors that decode payload into NudgeHostEvent variants.
/// Each extractor inspects the props object and returns Some event or None.
let private extractorMap : Map<string, obj -> NudgeHostEvent option> =
    Map [
        "session.next.prompted", fun props ->
            let prompt = Dyn.get props "prompt"
            let promptText = Dyn.str prompt "text"
            let text =
                if promptText <> "" then promptText
                else
                    let partsText = getPartsText (Dyn.get props "parts")
                    if partsText <> "" then partsText else Dyn.str props "text"
            Some (SessionNextPrompted text)

        "message.updated", fun props ->
            let info = Dyn.get props "info"
            let outcome =
                if isAbortDomainError (Dyn.get info "error") then UpdateAborted
                elif isCompletedAssistantMessage info then UpdateCompletedAssistant
                else UpdateNoChange
            Some (MessageUpdated outcome)

        "message.part.updated", fun props ->
            let part = Dyn.get props "part"
            let partType = Dyn.str part "type"
            let hasAbortError = 
                isAbortDomainError (Dyn.get part "error")
                || isAbortDomainError (Dyn.get part "state")
            let outcome =
                if partType = "retry" then PartRetry
                elif hasAbortError then PartAborted
                elif isRetryProgressPart partType then PartRetryProgress
                else PartOther
            Some (MessagePartUpdated outcome)

        "session.next.step.failed", fun props ->
            let errorObj = Dyn.get props "error"
            let outcome = if isAbortDomainError errorObj then StepFailAbort else StepFailOther
            Some (SessionNextStepFailed outcome)

        "session.next.tool.failed", fun props ->
            let errorObj = Dyn.get props "error"
            let outcome = if isAbortDomainError errorObj then ToolFailAbort else ToolFailOther
            Some (SessionNextToolFailed outcome)

        "session.next.step.ended", fun props ->
            let direct = Dyn.str props "finish"
            let finish = if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
            Some (SessionNextStepEnded finish)

        "session.error", fun props ->
            let errorObj = Dyn.get props "error"
            let outcome = if isAbortDomainError errorObj then SessionErrorAbort else SessionErrorOther
            Some (SessionError outcome)

        "session.status", fun props ->
            let ev =
                match Dyn.str (Dyn.get props "status") "type" with
                | "idle" -> SessionStatusIdle
                | "busy" -> SessionStatusBusy
                | "retry" -> SessionStatusRetry
                | _ -> Other
            Some ev
    ]

/// Decode any Opencode session event into the finite `NudgeHostEvent` DU. Pure
/// events short-circuit via `pureMap`; events whose outcome depends on the
/// payload use the per-type extractors. Unknown event types collapse to `Other`
/// — never throws, so `NudgeState.handleEvent` cannot be poisoned by a
/// malformed event payload.
let decodeNudgeHostEvent (eventType: string) (props: obj) : NudgeHostEvent =
    match Map.tryFind eventType pureMap with
    | Some ev -> ev
    | None ->
        match Map.tryFind eventType extractorMap with
        | Some extract -> extract props |> Option.defaultValue Other
        | None -> if isRetryProgressEvent eventType then RetryProgress else Other
