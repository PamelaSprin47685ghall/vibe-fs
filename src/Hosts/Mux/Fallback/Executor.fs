module Wanxiangshu.Hosts.Mux.Fallback.Executor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.Ports

type private MuxReceiptValidationResult =
    | ValidReceipt of messageId: string
    | InvalidReceipt of error: string
    | SimpleSuccess
    | SimpleFailure

let private validateMuxReceipt
    (result: obj)
    (expectedSessionId: string)
    (expectedDispatchId1: string)
    (expectedDispatchId2: string)
    : MuxReceiptValidationResult =
    if Dyn.isNullish result then
        InvalidReceipt "nudge returned nullish value"
    elif Dyn.typeIs result "boolean" then
        if unbox<bool> result then SimpleSuccess else SimpleFailure
    else
        let msgId = Dyn.str result "messageId"
        let msgId = if msgId <> "" then msgId else Dyn.str result "receiptId"
        let sessId = Dyn.str result "sessionId"

        let sessId =
            if sessId <> "" then
                sessId
            else
                Dyn.str result "workspaceId"

        let dispId = Dyn.str result "dispatchId"
        let dispId = if dispId <> "" then dispId else Dyn.str result "nonce"

        let dispId =
            if dispId <> "" then
                dispId
            else
                Dyn.str result "continuationId"

        let dispId =
            if dispId <> "" then
                dispId
            else
                Dyn.str result "continuationID"

        if sessId <> expectedSessionId then
            InvalidReceipt $"Receipt sessionId mismatch: expected {expectedSessionId}, got {sessId}"
        elif dispId <> expectedDispatchId1 && dispId <> expectedDispatchId2 then
            InvalidReceipt
                $"Receipt dispatchId mismatch: expected {expectedDispatchId1} or {expectedDispatchId2}, got {dispId}"
        elif msgId = "" then
            InvalidReceipt "Receipt messageId is empty"
        else
            ValidReceipt msgId

let private invokeNudge
    (helpers: obj)
    (workspaceId: string)
    (text: string)
    (continuationId: string)
    : JS.Promise<unit> =
    promise {
        if Dyn.isNullish helpers then
            return ()
        else
            let nudge = Dyn.get helpers "nudge"

            if Dyn.isNullish nudge then
                return ()
            else
                let! result =
                    (nudge $ (workspaceId, text, null, null, null, continuationId))
                    |> unbox<JS.Promise<obj>>

                let validation = validateMuxReceipt result workspaceId continuationId continuationId

                match validation with
                | ValidReceipt _ -> return ()
                | SimpleSuccess ->
                    return!
                        Promise.reject (
                            System.Exception(
                                "AcceptanceUnknown: nudge resolved true, cannot verify delivery without receipt"
                            )
                        )
                | SimpleFailure -> return! Promise.reject (System.Exception("Failed: nudge returned false"))
                | InvalidReceipt err -> return! Promise.reject (System.Exception("Failed: " + err))
    }

let private getChatHistory (helpers: obj) (workspaceId: string) : JS.Promise<obj array> =
    if Dyn.isNullish helpers then
        Promise.lift [||]
    else
        let getter = Dyn.get helpers "getChatHistory"

        if Dyn.isNullish getter then
            Promise.lift [||]
        else
            unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId)

let muxActionExecutor (helpers: obj) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | _ -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge helpers sessionID ("continue " + modelStr) continuationID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            let modelStr =
                match model.Variant with
                | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                | _ -> sprintf "%s/%s" model.ProviderID model.ModelID

            invokeNudge helpers sessionID (promptText + " " + modelStr) continuationID

        member _.FetchMessages sessionID = getChatHistory helpers sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(_sessionID: string) = Promise.lift None

        // Mux capacity downgrade: abort is not supported by the host
        // adapter, so we surface the typed failure rather than silently
        // returning Promise.lift ().
        member _.AbortRun(_sessionID: string) =
            Promise.reject (
                System.Exception("AbortUnavailable: Mux host adapter does not expose a session-level abort API")
            ) }
