module Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor

open Fable.Core
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutorDispatch
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection

let private sendContinueImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (directory: string)
    (sessionID: string)
    (model: FallbackModel)
    (continuationID: string)
    : JS.Promise<unit> =
    dispatchFallbackContinuation
        runtime
        client
        directory
        sessionID
        model
        "\u200B"
        ContinuationMode.ResumeInterruptedTurn
        continuationID

let private captureCurrentModelImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (sessionID: string)
    : JS.Promise<FallbackModel option> =
    promise {
        let! msgs = fetchMessagesImpl client sessionID

        match tryGetLatestUserModel msgs with
        | Some m -> return Some m
        | None ->
            match (runtime.GetSession sessionID).Model with
            | Some m -> return Some m
            | None -> return! tryReadCurrentModel client sessionID
    }

let private recoverWithPromptImpl
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (directory: string)
    (sessionID: string)
    (model: FallbackModel)
    (promptText: string)
    (continuationID: string)
    : JS.Promise<unit> =
    dispatchFallbackContinuation
        runtime
        client
        directory
        sessionID
        model
        promptText
        (ContinuationMode.RecoverToolCallText promptText)
        continuationID

let private abortRunImpl (client: obj) (sessionID: string) : JS.Promise<unit> =
    promise {
        let arg = box {| path = box {| id = sessionID |} |}
        do! invokeClient client "abort" arg |> Promise.map ignore
    }

let opencodeActionExecutorWithDir (runtime: FallbackRuntimeStore) (client: obj) (directory: string) : IActionExecutor =
    { new IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            sendContinueImpl runtime client directory sessionID model continuationID

        member _.FetchMessages sessionID = fetchMessagesImpl client sessionID
        member _.PropagateFailure _sessionID = Promise.lift ()

        member _.CaptureCurrentModel sessionID =
            captureCurrentModelImpl runtime client sessionID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            recoverWithPromptImpl runtime client directory sessionID model promptText continuationID

        member _.AbortRun sessionID = abortRunImpl client sessionID }

[<System.Obsolete("Use opencodeActionExecutorWithDir instead")>]
let opencodeActionExecutor (runtime: FallbackRuntimeStore) (client: obj) : IActionExecutor =
    opencodeActionExecutorWithDir runtime client ""