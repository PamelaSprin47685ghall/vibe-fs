module Wanxiangshu.Shell.FallbackEventBridge

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackKernel.StateMachine
open Wanxiangshu.Shell.FallbackMessageCodec
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Shell.PromiseQueue

// ---------------------------------------------------------------------------
// Host-facing interfaces
// ---------------------------------------------------------------------------

type IEventTranslator =
    abstract TranslateError  : obj -> FallbackEvent option
    abstract ExtractSessionID : obj -> string
    abstract IsSessionError  : obj -> bool
    abstract IsSessionIdle   : obj -> bool
    abstract IsSessionBusy   : obj -> bool
    abstract IsNewUserMessage: obj -> bool

type IActionExecutor =
    abstract SendContinue    : sessionID:string * model:FallbackModel -> JS.Promise<unit>
    abstract AbortSession    : sessionID:string -> JS.Promise<unit>
    abstract FetchMessages   : sessionID:string -> JS.Promise<obj array>
    abstract PropagateFailure: sessionID:string -> JS.Promise<unit>
    abstract CaptureCurrentModel : sessionID:string -> JS.Promise<FallbackModel option>

type ConfigLookup = (string -> FallbackConfig)

// ---------------------------------------------------------------------------
// Core handler
// ---------------------------------------------------------------------------

let handleEvent
    (translator    : IEventTranslator)
    (runtime       : FallbackRuntimeState)
    (configLookup  : ConfigLookup)
    (executor      : IActionExecutor)
    (rawEvent      : obj) : JS.Promise<FallbackHookResult> =

    promise {
        let sessionID = translator.ExtractSessionID rawEvent

        let eventOpt =
            match translator.TranslateError rawEvent with
            | Some _ as ev -> ev
            | None ->
                if translator.IsNewUserMessage rawEvent then Some FallbackEvent.NewUserMessage
                elif translator.IsSessionBusy rawEvent then Some FallbackEvent.SessionBusy
                elif translator.IsSessionIdle rawEvent then Some FallbackEvent.SessionIdle
                else None

        match eventOpt with
        | None ->
            return { Consumed = false; State = runtime.GetOrCreateState sessionID }

        | Some evt ->
            let state = runtime.GetOrCreateState sessionID
            let agentName = runtime.GetAgentName sessionID
            let cfg = configLookup agentName

            let! chain =
                promise {
                    let existing = runtime.GetChain sessionID
                    if not (List.isEmpty existing) then
                        return existing
                    else
                        let resolved =
                            Map.tryFind (normalizeAgentName agentName) cfg.AgentChains
                            |> Option.defaultValue cfg.DefaultChain
                        if not (List.isEmpty resolved) then
                            runtime.SetChain sessionID resolved
                            return resolved
                        else
                            let! currentModel = executor.CaptureCurrentModel sessionID
                            match currentModel with
                            | Some current ->
                                let single = [ current ]
                                runtime.SetChain sessionID single
                                return single
                            | None -> return []
                }

            if List.isEmpty chain then
                return { Consumed = false; State = state }
            else
                let ns, action = transition state evt cfg chain
                runtime.UpdateState sessionID ns

                let consumed =
                    match evt with
                    | FallbackEvent.SessionError _ ->
                        match ns.Phase with
                        | FallbackPhase.Exhausted -> false
                        | _ -> true
                    | _ -> false

                runtime.SetConsumed sessionID consumed

                match action with
                | FallbackAction.DoNothing -> ()
                | FallbackAction.SendContinue model ->
                    do! executor.SendContinue (sessionID, model)
                | FallbackAction.AbortAndResume model ->
                    do! executor.AbortSession sessionID
                    do! executor.SendContinue (sessionID, model)
                | FallbackAction.PropagateFailure ->
                    do! executor.PropagateFailure sessionID

                let mutable finalState = ns
                if evt = FallbackEvent.SessionIdle && ns.Phase = FallbackPhase.Idle && not ns.TaskComplete && not ns.Cancelled then
                    let! msgs = executor.FetchMessages sessionID
                    if allTodosCompleted msgs then
                        finalState <- { ns with TaskComplete = true }
                        runtime.UpdateState sessionID finalState

                return { Consumed = consumed; State = finalState }
    }

// ---------------------------------------------------------------------------
// Handler factory — per-session serial queue
// ---------------------------------------------------------------------------

let createHandler
    (translator   : IEventTranslator)
    (runtime      : FallbackRuntimeState)
    (configLookup : ConfigLookup)
    (executor     : IActionExecutor)
    : (obj -> JS.Promise<FallbackHookResult>) =

    let mutable queues = Map.ofList<string, SerialQueue> []

    fun (rawEvent: obj) ->
        promise {
            let sessionID = translator.ExtractSessionID rawEvent
            let queue =
                match Map.tryFind sessionID queues with
                | Some q -> q
                | None ->
                    let q = SerialQueue()
                    queues <- Map.add sessionID q queues
                    q
            let! result = queue.Enqueue(fun () -> handleEvent translator runtime configLookup executor rawEvent)
            return result
        }
