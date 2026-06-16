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

let private opencodeTodoWriteToolName = "todowrite"

let private invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> =
    unbox (target?(method)(arg))

let private setOutput (o: obj) (v: string) : unit =
    o?("output") <- v

let private resolvedUnitPromise () : JS.Promise<unit> = async { return () } |> Async.StartAsPromise

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

// ── Atomic state holder ──
//
// JavaScript is single-threaded, so a synchronous read-modify-write is atomic
// with respect to every other hook callback.  The holder must NEVER await
// inside a transition: that was the freeze.  The old serialized queue chained
// every hook onto one promise, and a transition that awaited `session.prompt`
// (a whole assistant turn) left that promise pending — jamming every later
// chat.message / command.execute.before behind it, and deadlocking outright
// when the nudge's own prompt re-entered chat.message.  All I/O now lives in
// detached flows outside this lock; transitions are pure and instant.

type private StateHolder<'state>(initialState: 'state) =
    let mutable state = initialState

    member _.Mutate<'result>(transition: 'state -> 'state * 'result) : 'result =
        let nextState, result = transition state
        state <- nextState
        result

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
                        match todoStatusOfString status with
                        | Some s when isTerminal s -> None
                        | _ -> Some status)
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

let private selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt
    | NudgeLoop -> Some loopNudgePrompt
    | _ -> None

let private sendNudge (client: obj) (sessionID: string) (agentOpt: string option) (promptText: string) : Async<unit> =
    async {
        let body = createPromptBody agentOpt promptText
        let promptArg = box {| path = box {| id = sessionID |}; body = body |}
        let session = Dyn.get client "session"
        do! invoke1 promptArg "prompt" session |> Async.AwaitPromise |> Async.Ignore
    }

// ── Nudge: claim (pure) → I/O (detached) → record (pure) ──
//
// Phase 1 `tryClaimNudge` runs under the lock: it checks the guards and, if the
// session is eligible, marks it nudged and returns true.  Holding the claim
// dedups concurrent idle/step events for the same session.
// Phase 2 (snapshot + send) runs OUTSIDE the lock in a detached async.
// Phase 3 (`decideNudge` re-validation, `recordSend`) re-enters the lock for
// instant pure updates only.

let private tryClaimNudge (state: NudgeShellState) (sessionID: string) : NudgeShellState * bool =
    if hasStoppedSession state sessionID
       || hasRetryPendingSession state sessionID
       || hasNudgedSession state sessionID then
        state, false
    else
        addNudgedSession state sessionID, true

/// The pure decision made after the snapshot is in hand, re-validating the
/// claim against any state changes that happened during the (lock-free) I/O.
type private NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option * messageCount: int option

let private decideNudge (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                        (state: NudgeShellState) (sessionID: string)
                        (snapshot: SessionSnapshot) : NudgeShellState * NudgeDecision =
    // The claim may have been dropped while we did I/O (user resumed, session
    // cleared, or an unexpected busy released it).  If so, stand down silently.
    if not (hasNudgedSession state sessionID) || hasStoppedSession state sessionID then
        state, StandDown
    else
        let state = rememberAgent state sessionID snapshot.agentFromMessage
        match snapshot.messageCount with
        | Some count when getDeliveredCount state sessionID = Some count ->
            deleteNudgedSession state sessionID, StandDown
        | _ ->
            let context =
                { todos = snapshot.todos
                  lastAssistantMessage = snapshot.lastAssistantMessage
                  hasActiveRunner = false
                  isLoopActive = reviewStore.isReviewActive(sessionID) }
            match decide context with
            | NudgeNone | NudgeRunner -> deleteNudgedSession state sessionID, StandDown
            | action ->
                match selectNudgePrompt action with
                | None -> deleteNudgedSession state sessionID, StandDown
                | Some promptText ->
                    let agentOpt =
                        getAgent state sessionID
                        |> Option.orElse (ChildAgent.lookupChildAgent sessionID)
                    let state = { state with lastNudgedSession = Some sessionID }
                    state, Send(promptText, agentOpt, snapshot.messageCount)

/// The pure state update applied after the send attempt resolves.
type private SendOutcome = Delivered of messageCount: int option | Aborted | Busy | Failed

let private recordSend (state: NudgeShellState) (sessionID: string) (outcome: SendOutcome) : NudgeShellState =
    match outcome with
    | Delivered count ->
        let state =
            match count with
            | Some c -> setDeliveredCount state sessionID c
            | None -> state
        deleteNudgedSession state sessionID
    | Aborted -> stopSession state sessionID
    | Busy -> deleteNudgedSession state sessionID
    | Failed -> addRetryPendingSession (deleteNudgedSession state sessionID) sessionID

/// The detached nudge flow: all client I/O happens here, never under the lock.
/// Each lock re-entry (`Mutate`) is a pure, instant transition.
let private runNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                         (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                         (sessionID: string) : Async<unit> =
    async {
        try
            let! snapshotOpt = collectSnapshot client sessionID
            match snapshotOpt with
            | None -> holder.Mutate(fun state -> deleteNudgedSession state sessionID, ())
            | Some snapshot ->
                match holder.Mutate(fun state -> decideNudge reviewStore state sessionID snapshot) with
                | StandDown -> ()
                | Send(promptText, agentOpt, messageCount) ->
                    let! caught = Async.Catch(sendNudge client sessionID agentOpt promptText)
                    let outcome =
                        match caught with
                        | Choice1Of2 () -> Delivered messageCount
                        | Choice2Of2 error ->
                            match translateJsError error with
                            | MessageAborted -> Aborted
                            | SessionBusy -> Busy
                            | _ -> Failed
                    holder.Mutate(fun state -> recordSend state sessionID outcome, ())
        with _ ->
            // Never let an unexpected throw strand the claim and re-block the session.
            holder.Mutate(fun state -> deleteNudgedSession state sessionID, ())
    }

/// Fire the nudge flow detached from the caller's hook promise.  `StartImmediate`
/// runs only up to the first `AwaitPromise` (kicking off the snapshot SDK call,
/// which is non-blocking) before yielding, so the hook returns at once and the
/// rest of the flow — including any `session.prompt` — never blocks the lock.
let private startNudgeFlow (holder: StateHolder<NudgeShellState>) (client: obj)
                           (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore)
                           (sessionID: string) : unit =
    Async.StartImmediate(runNudgeFlow holder client reviewStore sessionID)

// ── Event handlers ──

let private handleSessionDelete state sessionID = clearSession state sessionID

let private handleSessionNextPrompted state (props: obj) sessionID =
    let text =
        let prompt = Dyn.get props "prompt"
        let promptText = Dyn.str prompt "text"
        if promptText <> "" then promptText
        else
            let partsText = getPartsText (Dyn.get props "parts")
            if partsText <> "" then partsText else Dyn.str props "text"
    if isNudgePrompt text then state else resumeSession state sessionID

let private handleSessionNextRetried state sessionID = addRetryPendingSession state sessionID

let private handleMessageUpdated state (props: obj) sessionID =
    let info = Dyn.get props "info"
    if isAbortDomainError (Dyn.get info "error") then
        stopSession state sessionID, false
    elif isCompletedAssistantMessage info then
        tryClaimNudge state sessionID
    else
        state, false

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

let private handleSessionNextStepEnded state (props: obj) sessionID =
    let state = deleteRetryPendingSession state sessionID
    let finish =
        let direct = Dyn.str props "finish"
        if direct <> "" then direct else Dyn.str (Dyn.get props "info") "finish"
    if finish <> "" && isTerminalAssistantFinish finish then
        tryClaimNudge state sessionID
    else
        state, false

let private handleSessionIdle state sessionID = tryClaimNudge state sessionID

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

let private dispatchEvent state eventType (props: obj) sessionID : NudgeShellState * bool =
    match eventType with
    | "stream-abort" -> clearSession state sessionID, false
    | "session.delete" | "session.close" | "session.remove" | "session.deleted" ->
        handleSessionDelete state sessionID, false
    | "session.next.prompted" ->
        handleSessionNextPrompted state props sessionID, false
    | "session.next.retried" ->
        handleSessionNextRetried state sessionID, false
    | "message.updated" ->
        handleMessageUpdated state props sessionID
    | "message.part.updated" ->
        handleMessagePartUpdated state props sessionID, false
    | "session.next.step.failed" ->
        handleSessionNextStepFailed state props sessionID, false
    | "session.next.tool.failed" ->
        handleSessionNextToolFailed state props sessionID, false
    | "session.next.step.ended" ->
        handleSessionNextStepEnded state props sessionID
    | "session.idle" ->
        handleSessionIdle state sessionID
    | "session.error" ->
        handleSessionError state props sessionID, false
    | "session.status" ->
        let statusType = Dyn.str (Dyn.get props "status") "type"
        match statusType with
        | "idle" -> handleSessionIdle state sessionID
        | "busy" -> handleSessionBusy state sessionID, false
        | "retry" -> handleSessionRetryStatus state props sessionID, false
        | _ -> state, false
    | _ ->
        if isRetryProgressEvent eventType then
            handleRetryProgress state props sessionID, false
        else
            state, false

// ── Hook class ──

type NudgeHook(ctx: obj, reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) =
    let client = Dyn.get ctx "client"
    let holder = StateHolder<NudgeShellState>(emptyState ())

    member _.handleChatMessage(sessionID: string, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Mutate(fun state ->
            let text = getPartsText parts
            if isNudgePrompt text then state, ()
            else
                let agentOpt = if agent <> "" then Some agent else None
                resumeSession (rememberAgent state sessionID agentOpt) sessionID, ())
        resolvedUnitPromise ()

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let sessionID = Dyn.str input "sessionID"
        holder.Mutate(fun state -> resumeSession state sessionID, ())
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter(input: obj) (output: obj) : JS.Promise<unit> =
        async {
            if Dyn.str input "tool" = opencodeTodoWriteToolName then
                let out = Dyn.get output "output"
                if not (Dyn.isNullish out) && Dyn.typeIs out "string" then
                    let s = string out
                    if not (s.Contains meditatorNudge) then
                        setOutput output (s + "\n" + meditatorNudge)
        } |> Async.StartAsPromise

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        let claimed =
            holder.Mutate(fun state ->
                try
                    let event = Dyn.get input "event"
                    let eventType = Dyn.str event "type"
                    let rawProps = Dyn.get event "properties"
                    let props = if Dyn.isNullish rawProps then event else rawProps
                    let sessionID = getSessionID eventType props
                    if sessionID = "" then state, None
                    else
                        let nextState, wantsNudge = dispatchEvent state eventType props sessionID
                        nextState, (if wantsNudge then Some sessionID else None)
                with _ -> state, None)
        match claimed with
        | Some sessionID -> startNudgeFlow holder client reviewStore sessionID
        | None -> ()
        resolvedUnitPromise ()

let createNudgeHook (ctx: obj) (reviewStore: VibeFs.Kernel.ReviewRuntime.ReviewStore) : NudgeHook =
    NudgeHook(ctx, reviewStore)
