module Wanxiangshu.Runtime.NudgeRuntimeMuxHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Runtime.MuxLogicalReceipt

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

/// Re-export Mux logical receipt classification for nudge runtime callers.
let validateMuxReceipt
    (result: obj)
    (expectedSessionId: string)
    (expectedDispatchId1: string)
    (expectedDispatchId2: string)
    : MuxLogicalReceipt =
    classify result expectedSessionId expectedDispatchId1 expectedDispatchId2

let receiptToSendOutcome = toSendOutcome

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
