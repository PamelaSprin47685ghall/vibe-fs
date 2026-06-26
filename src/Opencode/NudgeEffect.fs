module Wanxiangshu.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.ErrorClassify

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

type StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState

    member _.Mutate<'result>(transition: 'state -> 'state * 'result) : 'result =
        let nextState, result = transition state
        state <- nextState
        result

let private collectSnapshot (client: obj) (sessionID: SessionId) : JS.Promise<SessionSnapshot option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID
            match getSessionApiFromClient client with
            | Error _ -> return None
            | Ok session ->
                let! todoResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "todo" session
                let openTodosFromApi = decodeTodos (Dyn.get todoResp "data")
                let! messagesResp = invoke1 (box {| path = {| id = sessionIDStr |} |}) "messages" session
                let messagesData = Dyn.get messagesResp "data"
                let openTodos =
                    if not (List.isEmpty openTodosFromApi) then openTodosFromApi
                    else recoverOpenTodosFromMessages messagesData
                let lastAssistantMessage, agentFromMessage, alreadyNudged =
                    decodeLastAssistant messagesData
                return Some { todos = openTodos
                              lastAssistantMessage = lastAssistantMessage
                              alreadyNudged = alreadyNudged
                              agentFromMessage = agentFromMessage }
        with _ ->
            return None
    }

let private sendNudge (client: obj) (sessionID: SessionId) (agentOpt: string option) (promptText: string) : JS.Promise<unit> =
    promise {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = Id.sessionIdValue sessionID |}; body = body |}
        match getSessionApiFromClient client with
        | Error _ -> ()
        | Ok session -> do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private runNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                          (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
                          (registry: ChildAgentRegistry)
                          (sessionID: SessionId) : JS.Promise<unit> =
    promise {
        try
            let! snapshotOpt = collectSnapshot client sessionID
            match snapshotOpt with
            | None -> holder.Mutate(fun (state: NudgeShellState) -> clearSession state (Id.sessionIdValue sessionID), ())
            | Some snapshot ->
                let sid = Id.sessionIdValue sessionID
                match holder.Mutate(fun (state: NudgeShellState) -> decideNudge reviewStore.isReviewActive registry.LookupChildAgent state sid snapshot) with
                | StandDown -> ()
                | Send(promptText, agentOpt) ->
                    let! caught = sendNudge client sessionID agentOpt promptText |> Promise.result
                    let outcome =
                        match caught with
                        | Ok () -> Delivered
                        | Error error ->
                            match translateJsError error with
                            | MessageAborted -> Aborted
                            | SessionBusy -> Busy
                            | _ -> Failed
                    holder.Mutate(fun (state: NudgeShellState) ->
                        match tryRecordSend state sid outcome with
                        | Some nextState -> nextState, ()
                        | None -> state, ())
        with _ ->
            holder.Mutate(fun (state: NudgeShellState) -> clearSession state (Id.sessionIdValue sessionID), ())
    }

let startNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
                    (registry: ChildAgentRegistry)
                    (sessionID: SessionId) : unit =
    runNudgeFlow holder client reviewStore registry sessionID |> Promise.start
