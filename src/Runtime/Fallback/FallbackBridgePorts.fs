module Wanxiangshu.Runtime.Fallback.FallbackBridgePorts

open Wanxiangshu.Runtime

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types

let getOrdinal (o: obj) : int =
    if Dyn.isNullish o then
        0
    elif Dyn.typeIs o "number" then
        unbox<int> o
    elif Dyn.typeIs o "string" then
        let s = unbox<string> o

        match System.Int32.TryParse s with
        | true, v -> v
        | _ -> 0
    else
        0

type IEventTranslator =
    abstract TranslateError: obj -> FallbackEvent option
    abstract ExtractSessionID: obj -> string
    abstract IsSessionError: obj -> bool
    abstract IsSessionIdle: obj -> bool
    abstract IsSessionBusy: obj -> bool
    abstract IsNewUserMessage: sessionID: string * rawEvent: obj -> bool
    abstract ExtractNewUserMessageId: rawEvent: obj -> string option
    abstract ExtractRoutingContext: rawEvent: obj -> (string option * string option)
    abstract IsAssistantMessage: rawEvent: obj -> bool
    abstract ExtractAssistantMessageId: rawEvent: obj -> string option
    abstract ExtractAssistantParentId: rawEvent: obj -> string option
    abstract ExtractContinuationIdentity: rawEvent: obj -> (string * int) option
    abstract ExtractHostRunId: rawEvent: obj -> string option
    abstract ExtractTurnObservation: rawEvent: obj -> TurnObservation option

type IActionExecutor =
    abstract SendContinue: sessionID: string * model: FallbackModel * continuationID: string -> JS.Promise<unit>
    abstract FetchMessages: sessionID: string -> JS.Promise<obj array>
    abstract PropagateFailure: sessionID: string -> JS.Promise<unit>
    abstract CaptureCurrentModel: sessionID: string -> JS.Promise<FallbackModel option>

    abstract RecoverWithPrompt:
        sessionID: string * model: FallbackModel * promptText: string * continuationID: string -> JS.Promise<unit>

    abstract AbortRun: sessionID: string -> JS.Promise<unit>

type ConfigLookup = (string -> FallbackConfig)
