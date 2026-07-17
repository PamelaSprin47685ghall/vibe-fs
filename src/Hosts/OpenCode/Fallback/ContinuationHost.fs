module Wanxiangshu.Hosts.Opencode.Fallback.ContinuationHost

open Fable.Core
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.ContinuationHost
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

/// Model-facing payload for a semantic-free continuation.
/// This is not a correlation key; identity comes from metadata and host receipts.
let private continuationPayload = "\u200B"

let private tryDecodeCreatedUserMessageId (response: obj) : string option =
    let id = Dyn.str response "id"

    if id <> "" then
        Some id
    else
        let data = Dyn.get response "data"

        if Dyn.isNullish data then
            None
        else
            let id2 = Dyn.str data "id"
            if id2 <> "" then Some id2 else None

let private fetchMessages (client: obj) (sessionId: string) : JS.Promise<obj array> =
    promise {
        let arg = box {| path = box {| id = sessionId |} |}
        let! resp = invokeClient client "messages" arg
        let data = Dyn.get resp "data"
        return if Dyn.isArray data then (data :?> obj array) else [||]
    }

let private findUserMessageIdByContinuationId (messages: obj array) (continuationId: string) : string option =
    messages
    |> Array.tryPick (fun msg ->
        let parts = Dyn.get msg "parts"

        if not (Dyn.isArray parts) then
            None
        else
            let partsArr = parts :?> obj array

            partsArr
            |> Array.tryPick (fun part ->
                let metadata = Dyn.get part "metadata"

                if Dyn.isNullish metadata then
                    None
                else
                    let ws = Dyn.get metadata "wanxiangshu"

                    if Dyn.isNullish ws then
                        None
                    else
                        let cid = Dyn.str ws "continuationId"

                        if cid = continuationId then
                            let messageId = Dyn.str msg "id"
                            if messageId <> "" then Some messageId else None
                        else
                            None))

let private dispatchImpl (client: obj) (request: ContinuationRequest) : JS.Promise<HostDispatchReceipt> =
    promise {
        let agent = if request.Agent <> "" then Some request.Agent else None

        let body = createFallbackContinuationPromptBody agent continuationPayload request

        let arg =
            box
                {| path = box {| id = request.SessionId |}
                   body = body |}

        let! response = invokeClient client "prompt" arg

        match tryDecodeCreatedUserMessageId response with
        | Some messageId -> return HostDispatchReceipt.UserMessageAccepted messageId
        | None -> return HostDispatchReceipt.OpaqueAccepted request.ContinuationId
    }

let private tryAbortOwnedImpl
    (client: obj)
    (_request: ContinuationRequest)
    (_receipt: HostDispatchReceipt)
    : JS.Promise<bool> =
    promise {
        // Ownership (generation, cancelGeneration, active continuation id) is
        // verified by the runtime before this host call is made.
        let arg = box {| path = box {| id = _request.SessionId |} |}
        let! _ = invokeClient client "abort" arg
        return true
    }

let private reconcileImpl (client: obj) (request: ContinuationRequest) : JS.Promise<HostDispatchReceipt option> =
    promise {
        let! messages = fetchMessages client request.SessionId

        match findUserMessageIdByContinuationId messages request.ContinuationId with
        | Some messageId -> return Some(HostDispatchReceipt.UserMessageAccepted messageId)
        | None -> return None
    }

let opencodeContinuationHost (client: obj) : IContinuationHost =
    { new IContinuationHost with
        member _.Dispatch request = dispatchImpl client request

        member _.TryAbortOwned(request, receipt) =
            tryAbortOwnedImpl client request receipt

        member _.Reconcile request = reconcileImpl client request }
