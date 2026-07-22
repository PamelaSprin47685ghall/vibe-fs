module Wanxiangshu.Hosts.Omp.Fallback.ActionExecutor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.RuntimeStore

let private tryGetSession (sessionID: string) (sessionApi: obj) : obj option =
    match Wanxiangshu.Hosts.Omp.ExecutorTools.ompScope.TryFindKey("omp_session_" + sessionID) with
    | Some s -> Some s
    | None ->
        if not (Dyn.isNullish sessionApi) then
            Some sessionApi
        else
            None

let private captureModel (session: obj) : FallbackModel option =
    let model = Dyn.get session "model"
    Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj model

let private captureAgent (session: obj) : string option =
    let agent = Dyn.str session "agent"
    if agent <> "" then Some agent else None

/// Prompt resolve is transport only — not HostAccepted (SPEC §4.5).
/// Require a verifiable message id; otherwise raise AcceptanceUnknown.
let private requirePromptReceipt (response: obj) : unit =
    match tryExtractMessageId response with
    | Some _ -> ()
    | None ->
        raise (
            System.Exception(
                "AcceptanceUnknown: OMP sessionPrompt resolved without message id; prompt complete ≠ accepted"
            )
        )

type OmpActionExecutorClass(runtime: FallbackRuntimeStore, sessionApi: obj) =
    let invoke (method_: string) (arg: obj) : JS.Promise<obj> =
        if Dyn.isNullish sessionApi then
            Promise.lift (unbox null)
        else
            unbox<JS.Promise<obj>> (sessionApi?(method_) (arg))

    let resolveModelAndAgent (fallbackModel: FallbackModel) (sessionID: string) =
        let sessionOpt = tryGetSession sessionID sessionApi
        let sessionAgentOpt = sessionOpt |> Option.bind captureAgent

        let modelOpt =
            formatModelString fallbackModel.ProviderID fallbackModel.ModelID fallbackModel.Variant

        let agent =
            match sessionAgentOpt with
            | Some sa -> Some sa
            | None ->
                let a = (runtime.GetSession sessionID).AgentName
                if a <> "" then Some a else None

        modelOpt, agent

    let fetchMessages (sessionID: string) : JS.Promise<obj array> =
        promise {
            let arg = box {| sessionId = sessionID |}
            let! resp = invoke "sessionMessages" arg
            let data = Dyn.get resp "data"
            return if Dyn.isArray data then (data :?> obj array) else [||]
        }

    let sendPrompt (sessionID: string) (text: string) (model: FallbackModel) (continuationID: string) =
        promise {
            let modelOpt, agentOpt = resolveModelAndAgent model sessionID
            let promptPayload = buildSessionPromptPayload text modelOpt (Some continuationID) agentOpt
            let! response = sessionPromptViaApi sessionApi sessionID promptPayload
            requirePromptReceipt response
        }

    interface IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            sendPrompt sessionID "\u200B" model continuationID

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            sendPrompt sessionID promptText model continuationID

        member _.AbortRun sessionID =
            promise {
                let arg = box {| sessionId = sessionID |}
                do! invoke "sessionAbort" arg |> Promise.map ignore
            }

        member _.FetchMessages sessionID = fetchMessages sessionID

        member _.PropagateFailure(_sessionID: string) = Promise.lift ()

        member _.CaptureCurrentModel(sessionID: string) =
            promise {
                let! msgs = fetchMessages sessionID

                match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.tryGetLatestUserModel msgs with
                | Some m -> return Some m
                | None ->
                    match (runtime.GetSession sessionID).Model with
                    | Some m -> return Some m
                    | None ->
                        match tryGetSession sessionID sessionApi with
                        | Some sess -> return captureModel sess
                        | None -> return None
            }

let ompActionExecutor (runtime: FallbackRuntimeStore) (sessionApi: obj) : IActionExecutor =
    OmpActionExecutorClass(runtime, sessionApi)
