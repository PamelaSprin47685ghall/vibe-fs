module Wanxiangshu.Runtime.MuxLogicalReceipt

open Fable.Core
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.Nudge.Types

/// Mux host nudge/continue return value mapped to a logical receipt.
/// Boolean true is never HostAccepted: the promise resolving is not proof
/// the host accepted a user message.
type MuxLogicalReceipt =
    | Accepted of DispatchAcceptance
    | AcceptanceUnknown of reason: string
    | Rejected of reason: string
    | Busy

let private readSessionId (result: obj) : string =
    let sessId = Dyn.str result "sessionId"
    if sessId <> "" then sessId else Dyn.str result "workspaceId"

let private readDispatchId (result: obj) : string =
    let dispId = Dyn.str result "dispatchId"
    let dispId = if dispId <> "" then dispId else Dyn.str result "nonce"
    let dispId = if dispId <> "" then dispId else Dyn.str result "continuationId"
    if dispId <> "" then dispId else Dyn.str result "continuationID"

let private readMessageId (result: obj) : string =
    let msgId = Dyn.str result "messageId"
    if msgId <> "" then msgId else Dyn.str result "receiptId"

/// Map a raw Mux nudge/continue return value to a typed logical receipt.
/// expectedDispatchId1/2 accept either the continuation id or the nudge nonce.
let classify
    (result: obj)
    (expectedSessionId: string)
    (expectedDispatchId1: string)
    (expectedDispatchId2: string)
    : MuxLogicalReceipt =
    if Dyn.isNullish result then
        Rejected "nudge returned nullish value"
    elif Dyn.typeIs result "boolean" then
        if unbox<bool> result then
            AcceptanceUnknown "nudge resolved true, cannot verify delivery without receipt"
        else
            Busy
    else
        let msgId = readMessageId result
        let sessId = readSessionId result
        let dispId = readDispatchId result

        if sessId <> expectedSessionId then
            Rejected $"Receipt sessionId mismatch: expected {expectedSessionId}, got {sessId}"
        elif dispId <> expectedDispatchId1 && dispId <> expectedDispatchId2 then
            Rejected
                $"Receipt dispatchId mismatch: expected {expectedDispatchId1} or {expectedDispatchId2}, got {dispId}"
        elif msgId = "" then
            AcceptanceUnknown "Receipt messageId is empty"
        else
            Accepted(UserMessageAccepted msgId)

let toSendOutcome (receipt: MuxLogicalReceipt) : SendOutcome =
    match receipt with
    | Accepted _ -> SendOutcome.Delivered
    | AcceptanceUnknown reason -> SendOutcome.AcceptanceUnknown reason
    | Busy -> SendOutcome.Busy
    | Rejected reason -> SendOutcome.Failed reason

let toDispatchAcceptance (receipt: MuxLogicalReceipt) : Result<DispatchAcceptance, exn> =
    match receipt with
    | Accepted acceptance -> Ok acceptance
    | AcceptanceUnknown reason -> Error(System.Exception("AcceptanceUnknown: " + reason))
    | Busy -> Error(System.Exception("Busy: session rejected nudge"))
    | Rejected reason -> Error(System.Exception("Failed: " + reason))

let abortUnavailableMessage : string =
    "AbortUnavailable: Mux host adapter does not expose a session-level abort API"

let abortUnavailableException () : exn =
    System.Exception(abortUnavailableMessage)

let isAcceptanceUnknownMessage (msg: string) : bool =
    msg.StartsWith("AcceptanceUnknown") || msg.Contains("AcceptanceUnknown:")

let isAbortUnavailableMessage (msg: string) : bool =
    msg.StartsWith("AbortUnavailable") || msg.Contains("AbortUnavailable:")
