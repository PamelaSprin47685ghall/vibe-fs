module Wanxiangshu.Hosts.Omp.Fallback.ActionExecutor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.Fallback.GateTransitions
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

type OmpActionExecutorClass(runtime: FallbackRuntimeStore, sessionApi: obj) =
    let invoke (method_: string) (arg: obj) : JS.Promise<obj> =
        if Dyn.isNullish sessionApi then
            Promise.lift (unbox null)
        else
            unbox<JS.Promise<obj>> (sessionApi?(method_) (arg))

    let resolveModelAndAgent (fallbackModel: FallbackModel) (sessionID: string) =
        let sessionOpt = tryGetSession sessionID sessionApi
        let sessionAgentOpt = sessionOpt |> Option.bind captureAgent
        let finalModel = fallbackModel

        let modelStr =
            match finalModel.Variant with
            | Some v -> sprintf "%s/%s:%s" finalModel.ProviderID finalModel.ModelID v
            | None -> sprintf "%s/%s" finalModel.ProviderID finalModel.ModelID

        let agent =
            match sessionAgentOpt with
            | Some sa -> sa
            | None -> runtime.GetAgentName sessionID

        modelStr, agent

    let fetchMessages (sessionID: string) : JS.Promise<obj array> =
        promise {
            let arg = box {| sessionId = sessionID |}
            let! resp = invoke "sessionMessages" arg
            let data = Dyn.get resp "data"
            return if Dyn.isArray data then (data :?> obj array) else [||]
        }

    interface IActionExecutor with
        member _.SendContinue(sessionID, model, continuationID) =
            promise {
                let modelStr, agent = resolveModelAndAgent model sessionID

                let pObj =
                    let p =
                        {| text = "\u200B"
                           model = modelStr
                           continuationID = continuationID |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}
                let arg = box {| sessionId = sessionID; body = body |}
                do! invoke "sessionPrompt" arg |> Promise.map ignore
            }

        member _.RecoverWithPrompt(sessionID, model, promptText, continuationID) =
            promise {
                let modelStr, agent = resolveModelAndAgent model sessionID

                let pObj =
                    let p =
                        {| text = promptText
                           model = modelStr
                           continuationID = continuationID |}

                    if agent <> "" then Dyn.withKey p "agent" agent else box p

                let body = box {| prompt = pObj |}
                let arg = box {| sessionId = sessionID; body = body |}
                do! invoke "sessionPrompt" arg |> Promise.map ignore
            }

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
                    match runtime.GetModel sessionID with
                    | Some m -> return Some m
                    | None ->
                        match tryGetSession sessionID sessionApi with
                        | Some sess -> return captureModel sess
                        | None -> return None
            }

let ompActionExecutor (runtime: FallbackRuntimeStore) (sessionApi: obj) : IActionExecutor =
    OmpActionExecutorClass(runtime, sessionApi)
