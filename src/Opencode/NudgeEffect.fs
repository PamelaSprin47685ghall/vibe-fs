module VibeFs.Opencode.NudgeEffect

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.HostTools
open VibeFs.Shell
open VibeFs.Shell.Dyn
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.ErrorClassify
open VibeFs.Opencode.NudgeEventCodec

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let setOutput (o: obj) (v: string) : unit =
    o?("output") <- v

let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

type StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState

    member _.Mutate<'result>(transition: 'state -> 'state * 'result) : 'result =
        let nextState, result = transition state
        state <- nextState
        result

let private recoverOpenTodosFromMessages (messagesData: obj) : string list =
    if not (Dyn.isArray messagesData) then []
    else
        (messagesData :?> obj array)
        |> Array.rev
        |> Array.tryPick (fun message ->
            let parts = Dyn.get message "parts"
            if not (Dyn.isArray parts) then None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    if Dyn.str part "type" <> "tool" || Dyn.str part "tool" <> "task" then None
                    else
                        let state = Dyn.get part "state"
                        let input = Dyn.get state "input"
                        let todos = Dyn.get input "todos"
                        if not (Dyn.isArray todos) then None
                        else
                            let openItems =
                                (todos :?> obj array)
                                |> Array.choose (fun todo ->
                                    let content = Dyn.str todo "content"
                                    let status = Dyn.str todo "status"
                                    if content = "" || status = "" then None
                                    else
                                        match todoStatusOfString status with
                                        | Some s when isTerminal s -> None
                                        | _ -> Some content)
                            Some openItems))
        |> Option.defaultValue [||]
        |> Array.toList

let private collectSnapshot (_holder: StateHolder<NudgeShellState>) (client: obj) (sessionID: SessionId) : JS.Promise<SessionSnapshot option> =
    promise {
        try
            let sessionIDStr = Id.sessionIdValue sessionID
            let session = Dyn.get client "session"
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
        let session = Dyn.get client "session"
        do! invoke1 promptArg "prompt" session |> Promise.map ignore
    }

let private runNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                          (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                          (registry: ChildAgentRegistry)
                          (sessionID: SessionId) : JS.Promise<unit> =
    promise {
        try
            let! snapshotOpt = collectSnapshot holder client sessionID
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
                    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
                    (registry: ChildAgentRegistry)
                    (sessionID: SessionId) : unit =
    runNudgeFlow holder client reviewStore registry sessionID |> Promise.start
