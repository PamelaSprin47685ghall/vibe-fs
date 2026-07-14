module Wanxiangshu.Opencode.SubsessionHostAdapter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.SubsessionActor
open Wanxiangshu.Shell.SubsessionTranscript

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

/// Pending turn-start receipts keyed by turnId (nonce).
/// ChatHooks resolves these when it sees a matching synthetic user message.
module PendingTurnReceipt =
    let mutable private pending =
        Map.empty<string, (HostStartReceipt -> unit) * (exn -> unit)>

    let register (turnId: string) : JS.Promise<HostStartReceipt> =
        Promise.create (fun resolve reject -> pending <- Map.add turnId (resolve, reject) pending)

    let tryResolve (turnId: string) (receipt: HostStartReceipt) : bool =
        match Map.tryFind turnId pending with
        | Some(resolve, _) ->
            pending <- Map.remove turnId pending
            resolve receipt
            true
        | None -> false

    let tryReject (turnId: string) (ex: exn) : unit =
        match Map.tryFind turnId pending with
        | Some(_, reject) ->
            pending <- Map.remove turnId pending
            reject ex
        | None -> ()

/// OpenCode host adapter.
/// Dispatch sends the prompt with metadata.nonce = TurnId.
/// Receipt arrives from ChatHooks via PendingTurnReceipt — never forged
/// from the prompt Promise alone.
type OpencodeSubsessionHost(client: obj, agent: string, _directory: string) =
    interface ISubsessionHost with
        member _.Dispatch(sessionId, turn) =
            promise {
                let model = turn.Model

                let modelStr =
                    match model.Variant with
                    | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                    | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                let nonce = TurnId.value turn.TurnId

                let body =
                    createPromptBodyWithModelAndNonce (Some agent) (Some modelStr) turn.Prompt (Some nonce)

                let arg =
                    box
                        {| path = box {| id = SessionId.value sessionId |}
                           body = body |}

                let receiptPromise = PendingTurnReceipt.register nonce

                try
                    match getSessionApiFromClient client with
                    | Ok session ->
                        try
                            let! _ = invoke1 arg "prompt" session
                            let! receipt = receiptPromise
                            return Ok receipt
                        with ex ->
                            PendingTurnReceipt.tryReject nonce ex

                            return
                                Error
                                    { ErrorName = "DispatchFailed"
                                      DomainError = None
                                      Message = ex.Message
                                      StatusCode = None
                                      IsRetryable = Some true }
                    | Error derr ->
                        let msg =
                            match derr with
                            | InvalidIntent(_, _, m) -> m
                            | _ -> "session API missing"

                        PendingTurnReceipt.tryReject nonce (exn msg)

                        return
                            Error
                                { ErrorName = "DispatchFailed"
                                  DomainError = Some derr
                                  Message = msg
                                  StatusCode = None
                                  IsRetryable = Some false }
                with ex ->
                    PendingTurnReceipt.tryReject nonce ex

                    return
                        Error
                            { ErrorName = "DispatchFailed"
                              DomainError = None
                              Message = ex.Message
                              StatusCode = None
                              IsRetryable = Some true }
            }

        member _.Abort(sessionId, _turnId) =
            promise {
                match getSessionApiFromClient client with
                | Ok session ->
                    let arg = box {| path = box {| id = SessionId.value sessionId |} |}
                    let! _ = invoke1 arg "abort" session
                    return ()
                | Error _ -> return ()
            }

        member _.ReadTranscript(sessionId) =
            promise {
                match getSessionApiFromClient client with
                | Ok session ->
                    let! resp = invoke1 (box {| path = box {| id = SessionId.value sessionId |} |}) "messages" session

                    let data = Dyn.get resp "data"

                    let msgs = if Dyn.isArray data then unbox<obj array> data else [||]

                    return buildTranscriptSnapshot msgs
                | Error _ ->
                    return
                        { AllTodosCompleted = false
                          ToolCallAsTextRecoveryPrompt = None
                          LastAssistantToolFinish = false
                          HasToolResultAfterLastAssistant = false
                          LastAssistantText = "" }
            }

        member _.AppendEvents(_events) = Promise.lift ()

let createHost (client: obj) (agent: string) (directory: string) : ISubsessionHost =
    OpencodeSubsessionHost(client, agent, directory) :> ISubsessionHost
