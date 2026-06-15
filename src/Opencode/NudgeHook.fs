module VibeFs.Opencode.NudgeHook

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.DomainError
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeEvents
open VibeFs.Kernel.Prompts

[<Emit("$2[$1]($0)")>]
let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = jsNative

[<Emit("$0.output = $1")>]
let private setOutput (o: obj) (v: string) : unit = jsNative

[<Emit("Promise.resolve()")>]
let private resolvedUnitPromise () : JS.Promise<unit> = jsNative

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if eventType = "session.created" || eventType = "session.updated" || eventType = "session.deleted" then
              Dyn.str info "id"
          else "" ]
    candidates |> List.tryFind (fun s -> s <> "") |> Option.defaultValue ""

let getPartsText (parts: obj) : string =
    if not (Dyn.isArray parts) then ""
    else
        (parts :?> obj array)
        |> Array.choose (fun part ->
            if Dyn.str part "type" = "text" then
                let text = Dyn.get part "text"
                if Dyn.isNullish text then None else Some (string text)
            else None)
        |> String.concat "\n"

let isCompletedAssistantMessage (info: obj) : bool =
    if Dyn.isNullish info then false
    else
        let isAssistant = Dyn.str info "role" = "assistant" || Dyn.str info "type" = "assistant"
        let hasError = not (Dyn.isNullish (Dyn.get info "error"))
        if not isAssistant || hasError then false
        else
            let finishVal = Dyn.get info "finish"
            if not (Dyn.isNullish finishVal) && Dyn.typeIs finishVal "string" then
                isTerminalAssistantFinish (string finishVal)
            else
                let timeCompleted = Dyn.get (Dyn.get info "time") "completed"
                not (Dyn.isNullish timeCompleted) && Dyn.typeIs timeCompleted "number"

let private isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false

// ── Immutable shell state ──

type private NudgeShellState =
    { nudgedSessions: Set<string>
      stoppedSessions: Set<string>
      retryPendingSessions: Set<string>
      sessionAgents: Map<string, string>
      lastNudgedSession: string option
      deliveredCounts: Map<string, int> }

let private emptyState () =
    { nudgedSessions = Set.empty
      stoppedSessions = Set.empty
      retryPendingSessions = Set.empty
      sessionAgents = Map.empty
      lastNudgedSession = None
      deliveredCounts = Map.empty }

let private hasStoppedSession state sessionID = Set.contains sessionID state.stoppedSessions
let private hasRetryPendingSession state sessionID = Set.contains sessionID state.retryPendingSessions
let private hasNudgedSession state sessionID = Set.contains sessionID state.nudgedSessions
let private getAgent state sessionID = Map.tryFind sessionID state.sessionAgents
let private getDeliveredCount state sessionID = Map.tryFind sessionID state.deliveredCounts

let private resumeSession state sessionID =
    { state with
        nudgedSessions = Set.remove sessionID state.nudgedSessions
        retryPendingSessions = Set.remove sessionID state.retryPendingSessions
        stoppedSessions = Set.remove sessionID state.stoppedSessions
        deliveredCounts = Map.remove sessionID state.deliveredCounts
        lastNudgedSession = if state.lastNudgedSession = Some sessionID then None else state.lastNudgedSession }

let private rememberAgent state sessionID agentOpt =
    match agentOpt with
    | Some agent when agent <> "" -> { state with sessionAgents = Map.add sessionID agent state.sessionAgents }
    | _ -> state

let private stopSession state sessionID =
    { state with
        nudgedSessions = Set.add sessionID state.nudgedSessions
        retryPendingSessions = Set.remove sessionID state.retryPendingSessions
        stoppedSessions = Set.add sessionID state.stoppedSessions
        lastNudgedSession = if state.lastNudgedSession = Some sessionID then None else state.lastNudgedSession }

let private clearSession state sessionID =
    let next = resumeSession state sessionID
    { next with
        sessionAgents = Map.remove sessionID next.sessionAgents
        deliveredCounts = Map.remove sessionID next.deliveredCounts }

let private setDeliveredCount state sessionID count =
    { state with deliveredCounts = Map.add sessionID count state.deliveredCounts }

let private addNudgedSession state sessionID =
    { state with nudgedSessions = Set.add sessionID state.nudgedSessions }

let private deleteNudgedSession state sessionID =
    { state with nudgedSessions = Set.remove sessionID state.nudgedSessions }

let private addRetryPendingSession state sessionID =
    { state with retryPendingSessions = Set.add sessionID state.retryPendingSessions }

let private deleteRetryPendingSession state sessionID =
    { state with retryPendingSessions = Set.remove sessionID state.retryPendingSessions }

// ── Serialized state holder ──

type private StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState
    let mutable tail : JS.Promise<unit> = resolvedUnitPromise()

    member _.Enqueue(transition: 'state -> Async<'state>) : Async<'state> =
        Async.FromContinuations(fun (ok, err, _) ->
            let previous = tail
            let next =
                async {
                    do! previous |> Async.AwaitPromise
                    try
                        let! nextState = transition state
                        state <- nextState
                        ok nextState
                    with ex -> err ex
                }
                |> Async.StartAsPromise
            tail <- next |> Async.AwaitPromise |> Async.Ignore |> Async.StartAsPromise)

// ── Snapshot ──

type private SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      messageCount: int option
      agentFromMessage: string option }

let private collectSnapshot (client: obj) (sessionID: string) : Async<SessionSnapshot option> =
    async {
        try
            let session = Dyn.get client "session"
            let! todoResp = invoke1 (box {| path = {| id = sessionID |} |}) "todo" session |> Async.AwaitPromise
            let todosData = Dyn.get todoResp "data"
            let openTodos =
                if Dyn.isArray todosData then
                    (todosData :?> obj array)
                    |> Array.choose (fun todo ->
                        let status = Dyn.str todo "status"
                        if terminalTodoStatuses.Contains status then None else Some status)
                    |> Array.toList
                else []
            let mutable lastAssistantMessage = ""
            let mutable messageCount : int option = None
            let mutable agentFromMessage : string option = None
            try
                let! messagesResp = invoke1 (box {| path = {| id = sessionID |} |}) "messages" session |> Async.AwaitPromise
                let messagesData = Dyn.get messagesResp "data"
                if Dyn.isArray messagesData then
                    let messagesArr = messagesData :?> obj array
                    messageCount <- Some messagesArr.Length
                    let lastAssistant =
                        messagesArr
                        |> Array.tryFindBack (fun msg -> isCompletedAssistantMessage (Dyn.get msg "info"))
                    match lastAssistant with
                    | Some msg ->
                        let info = Dyn.get msg "info"
                        let agentVal = Dyn.get info "agent"
                        if not (Dyn.isNullish agentVal) then agentFromMessage <- Some (string agentVal)
                        lastAssistantMessage <- getPartsText (Dyn.get msg "parts")
                    | None -> ()
            with _ -> ()
            return Some { todos = openTodos
                          lastAssistantMessage = lastAssistantMessage
                          messageCount = messageCount
                          agentFromMessage = agentFromMessage }
        with _ -> return None
    }

// ── Nudge dispatch ──

let private selectNudgePromptText (action: string) : string option =
    if action = "nudge-todo" then Some todoNudgePrompt
    elif action = "nudge-loop" then Some loopNudgePrompt
    else None

let private sendNudge (client: obj) (sessionID: string) (agentOpt: string option) (promptText: string) : Async<unit> =
    async {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = sessionID |}; body = body |}
        let session = Dyn.get client "session"
        do! invoke1 promptArg "prompt" session |> Async.AwaitPromise |> Async.Ignore
    }

let private nudgeIfNeeded (client: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                          (state: NudgeShellState) (sessionID: string) : Async<NudgeShellState> =
    async {
        if hasStoppedSession state sessionID then return state
        elif hasRetryPendingSession state sessionID then return state
        elif hasNudgedSession state sessionID then return state
        else
            let state = addNudgedSession state sessionID
            let! snapshotOpt = collectSnapshot client sessionID
            match snapshotOpt with
            | None -> return deleteNudgedSession state sessionID
            | Some snapshot ->
                let state = rememberAgent state sessionID snapshot.agentFromMessage
                match snapshot.messageCount with
                | Some count when getDeliveredCount state sessionID = Some count ->
                    return deleteNudgedSession state sessionID
                | _ ->
                    let context =
                        { todos = snapshot.todos
                          lastAssistantMessage = snapshot.lastAssistantMessage
                          hasActiveRunner = false
                          isLoopActive = reviewStore.isReviewActive(sessionID) }
                    match defaultCoordinator.shouldNudge(sessionID, context) with
                    | "none" -> return deleteNudgedSession state sessionID
                    | action ->
                        match selectNudgePromptText action with
                        | None -> return deleteNudgedSession state sessionID
                        | Some promptText ->
                            let agentOpt =
                                getAgent state sessionID
                                |> Option.orElse (ChildAgent.lookupChildAgent sessionID)
                            let state = { state with lastNudgedSession = Some sessionID }
                            try
                                do! sendNudge client sessionID agentOpt promptText
                                let state =
                                    match snapshot.messageCount with
                                    | Some count -> setDeliveredCount state sessionID count
                                    | None -> state
                                return deleteNudgedSession state sessionID
                            with error ->
                                match translateJsError error with
                                | MessageAborted -> return stopSession state sessionID
                                | SessionBusy -> return deleteNudgedSession state sessionID
                                | _ -> return addRetryPendingSession (deleteNudgedSession state sessionID) sessionID
    }

// ── Event handlers ──

let private handleSessionDelete state sessionID = clearSession state sessionID

let private handleSessionNextPrompted state (props: obj) sessionID =
    let text =
        let partsText = getPartsText (Dyn.get props "parts")
        if partsText <> "" then partsText else Dyn.str props "text"
    if isNudgePrompt text then state else resumeSession state sessionID

let private handleSessionNextRetried state sessionID = addRetryPendingSession state sessionID

let private handleMessageUpdated client reviewStore state (props: obj) sessionID =
    async {
        let info = Dyn.get props "info"
        if isAbortDomainError (Dyn.get info "error") then
            return stopSession state sessionID
        elif isCompletedAssistantMessage info then
            return! nudgeIfNeeded client reviewStore state sessionID
        else
            return state
    }

let private handleMessagePartUpdated state (props: obj) sessionID =
    let part = Dyn.get props "part"
    if Dyn.str part "type" = "retry" then
        addRetryPendingSession state sessionID
    elif isAbortDomainError (Dyn.get part "error") || isAbortDomainError (Dyn.get part "state") then
        stopSession state sessionID
    elif isRetryProgressPart (Dyn.str part "type") then
        deleteRetryPendingSession state sessionID
    else state

let private handleSessionNextStepFailed state (props: obj) sessionID =
    if isAbortDomainError (Dyn.get props "error") then stopSession state sessionID else state

let private handleSessionNextToolFailed state (props: obj) sessionID =
    if isAbortDomainError (Dyn.get props "error") then
        stopSession state sessionID
    else
        deleteRetryPendingSession state sessionID

let private handleSessionNextStepEnded client reviewStore state (props: obj) sessionID =
    async {
        let state = deleteRetryPendingSession state sessionID
        let finish =
            let direct = Dyn.str props "finish"
            if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
        if finish <> "" && isTerminalAssistantFinish finish then
            return! nudgeIfNeeded client reviewStore state sessionID
        else
            return state
    }

let private handleSessionIdle client reviewStore state _props sessionID =
    nudgeIfNeeded client reviewStore state sessionID

let private handleSessionBusy state sessionID =
    let state = if state.lastNudgedSession <> Some sessionID then deleteNudgedSession state sessionID else state
    { state with lastNudgedSession = None }

let private handleSessionError state (props: obj) sessionID =
    if isAbortDomainError (Dyn.get props "error") then
        stopSession state sessionID
    else
        addRetryPendingSession state sessionID

let private handleSessionRetryStatus state _props sessionID = addRetryPendingSession state sessionID

let private handleRetryProgress state _props sessionID = deleteRetryPendingSession state sessionID

let private dispatchEvent client reviewStore state eventType (props: obj) sessionID =
    async {
        match eventType with
        | "stream-abort" -> return clearSession state sessionID
        | "session.delete" | "session.close" | "session.remove" | "session.deleted" ->
            return handleSessionDelete state sessionID
        | "session.next.prompted" ->
            return handleSessionNextPrompted state props sessionID
        | "session.next.retried" ->
            return handleSessionNextRetried state sessionID
        | "message.updated" ->
            return! handleMessageUpdated client reviewStore state props sessionID
        | "message.part.updated" ->
            return handleMessagePartUpdated state props sessionID
        | "session.next.step.failed" ->
            return handleSessionNextStepFailed state props sessionID
        | "session.next.tool.failed" ->
            return handleSessionNextToolFailed state props sessionID
        | "session.next.step.ended" ->
            return! handleSessionNextStepEnded client reviewStore state props sessionID
        | "session.idle" ->
            return! handleSessionIdle client reviewStore state props sessionID
        | "session.error" ->
            return handleSessionError state props sessionID
        | "session.status" ->
            let statusType = Dyn.str (Dyn.get props "status") "type"
            match statusType with
            | "idle" -> return! handleSessionIdle client reviewStore state props sessionID
            | "busy" -> return handleSessionBusy state sessionID
            | "retry" -> return handleSessionRetryStatus state props sessionID
            | _ -> return state
        | _ ->
            if isRetryProgressEvent eventType then
                return handleRetryProgress state props sessionID
            else
                return state
    }

// ── Hook class ──

type NudgeHook(ctx: obj, reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) =
    let client = Dyn.get ctx "client"
    let holder = StateHolder<NudgeShellState>(emptyState ())

    member _.handleChatMessage(sessionID: string, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Enqueue(fun state ->
            async {
                let text = getPartsText parts
                if isNudgePrompt text then return state
                else
                    let agentOpt = if agent <> "" then Some agent else None
                    let state = rememberAgent state sessionID agentOpt
                    return resumeSession state sessionID
            })
        |> Async.Ignore
        |> Async.StartAsPromise

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let sessionID = Dyn.str input "sessionID"
        holder.Enqueue(fun state -> async { return resumeSession state sessionID })
        |> Async.Ignore
        |> Async.StartAsPromise

    member _.handleToolExecuteAfter(input: obj) (output: obj) : JS.Promise<unit> =
        async {
            if Dyn.str input "tool" = "todowrite" then
                let out = Dyn.get output "output"
                if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                    let s = string out
                    if not (s.Contains reverieNudge) then
                        setOutput output (s + "\n" + reverieNudge)
        } |> Async.StartAsPromise

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        holder.Enqueue(fun state ->
            async {
                try
                    let event = Dyn.get input "event"
                    let eventType = Dyn.str event "type"
                    let rawProps = Dyn.get event "properties"
                    let props = if Dyn.isNullish rawProps then event else rawProps
                    let sessionID = getSessionID eventType props
                    if sessionID = "" then return state
                    else return! dispatchEvent client reviewStore state eventType props sessionID
                with _ -> return state
            })
        |> Async.Ignore
        |> Async.StartAsPromise

let createNudgeHook (ctx: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : NudgeHook =
    NudgeHook(ctx, reviewStore)
