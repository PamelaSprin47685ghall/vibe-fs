module Wanxiangshu.Runtime.NudgeRuntimeMuxHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.NudgeProjection

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

type MuxReceiptValidationResult =
    | ValidReceipt of messageId: string
    | InvalidReceipt of error: string
    | SimpleSuccess
    | SimpleFailure

let validateMuxReceipt
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

let tryGetTodos (helpers: obj) (workspaceId: string) : JS.Promise<string list> =
    promise {
        try
            let getTodosFn = Dyn.get helpers "getTodos"

            if Dyn.typeIs getTodosFn "function" then
                let! result = unbox<JS.Promise<obj>> (Dyn.call1 getTodosFn workspaceId)

                if Dyn.isArray result then
                    return (result :?> obj array) |> Array.map string |> List.ofArray
                else
                    return []
            else
                return []
        with _ ->
            return []
    }

let getRootDirectory (workspaceDirectory: string) : string =
    if workspaceDirectory <> "" then
        workspaceDirectory
    else
        unbox<string> (nodeProcess?cwd ())

let getBlockStatus (snapshot: NudgeSnapshotState) (currentAnchor: string) : NudgeBlockStatus =
    let dedup: NudgeDedupState =
        { PendingNudge = snapshot.pendingNudge
          LastDispatchedAnchor = snapshot.lastDispatchedAnchor }

    if isBlocked dedup currentAnchor then
        NudgeBlockStatus.Blocked
    else
        NudgeBlockStatus.Allowed
