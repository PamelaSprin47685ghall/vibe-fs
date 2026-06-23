module VibeFs.Kernel.NudgeState

open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.PromptFragments

type NudgeShellState =
    { nudgedSessions: Set<string>
      stoppedSessions: Set<string>
      retryPendingSessions: Set<string>
      sessionAgents: Map<string, string>
      lastNudgedSession: string option }

/// Snapshot of a session at the moment a nudge is considered. `alreadyNudged`
/// is read straight from the dialogue history — true iff a nudge prompt already
/// follows the last completed assistant turn — so per-stop de-duplication
/// survives a process restart and never relies on an in-memory counter.
type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      alreadyNudged: bool
      agentFromMessage: string option }

type NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option

type SendOutcome =
    | Delivered
    | Aborted
    | Busy
    | Failed

/// Resolved outcome of a `message.updated` event. Replaces the
/// `(isAbortError, isCompletedAssistant)` boolean pair, which could express the
/// impossible (abort AND completed) state — the three legal states are now the
/// only constructors (P40/P41). Cases are prefixed `Update…` to avoid collision
/// with `DomainError.MessageAborted`.
type MessageOutcome =
    | UpdateAborted
    | UpdateCompletedAssistant
    | UpdateNoChange

/// Resolved outcome of a `message.part.updated` event. The host decodes the raw
/// `(partType, errorValue, stateValue)` triple into exactly one of these, so the
/// kernel transition never re-parses partType or re-checks abort flags.
type PartOutcome =
    | PartRetry
    | PartAborted
    | PartRetryProgress
    | PartOther

type StepFailOutcome = StepFailAbort | StepFailOther
type ToolFailOutcome = ToolFailAbort | ToolFailOther
type SessionErrorOutcome = SessionErrorAbort | SessionErrorOther

/// Decoded host event ready for exhaustive nudge state transition. Host adapters
/// decode their native event shapes into this DU; NudgeState owns the transitions.
type NudgeHostEvent =
    | StreamAbort
    | SessionDeleted
    | SessionNextPrompted of promptText: string
    | SessionNextRetried
    | MessageUpdated of outcome: MessageOutcome
    | MessagePartUpdated of outcome: PartOutcome
    | SessionNextStepFailed of outcome: StepFailOutcome
    | SessionNextToolFailed of outcome: ToolFailOutcome
    | SessionNextStepEnded of finish: string
    | SessionIdle
    | SessionError of outcome: SessionErrorOutcome
    | SessionStatusIdle
    | SessionStatusBusy
    | SessionStatusRetry
    | RetryProgress
    | Other

let emptyState =
    { nudgedSessions = Set.empty
      stoppedSessions = Set.empty
      retryPendingSessions = Set.empty
      sessionAgents = Map.empty
      lastNudgedSession = None }

let private hasStoppedSession state sessionID = Set.contains sessionID state.stoppedSessions
let private hasRetryPendingSession state sessionID = Set.contains sessionID state.retryPendingSessions
let private hasNudgedSession state sessionID = Set.contains sessionID state.nudgedSessions
let private getAgent state sessionID = Map.tryFind sessionID state.sessionAgents

let resumeSession state sessionID =
    { state with
        nudgedSessions = Set.remove sessionID state.nudgedSessions
        retryPendingSessions = Set.remove sessionID state.retryPendingSessions
        stoppedSessions = Set.remove sessionID state.stoppedSessions
        lastNudgedSession = if state.lastNudgedSession = Some sessionID then None else state.lastNudgedSession }

let rememberAgent state sessionID agentOpt =
    match agentOpt with
    | Some agent when agent <> "" -> { state with sessionAgents = Map.add sessionID agent state.sessionAgents }
    | _ -> state

let stopSession state sessionID =
    { state with
        nudgedSessions = Set.add sessionID state.nudgedSessions
        retryPendingSessions = Set.remove sessionID state.retryPendingSessions
        stoppedSessions = Set.add sessionID state.stoppedSessions
        lastNudgedSession = if state.lastNudgedSession = Some sessionID then None else state.lastNudgedSession }

let clearSession state sessionID =
    let next = resumeSession state sessionID
    { next with sessionAgents = Map.remove sessionID next.sessionAgents }

let addRetryPendingSession state sessionID =
    { state with retryPendingSessions = Set.add sessionID state.retryPendingSessions }

let deleteRetryPendingSession state sessionID =
    { state with retryPendingSessions = Set.remove sessionID state.retryPendingSessions }

let deleteNudgedSession state sessionID =
    { state with nudgedSessions = Set.remove sessionID state.nudgedSessions }

let tryClaimNudge state sessionID =
    if hasStoppedSession state sessionID
       || hasRetryPendingSession state sessionID
       || hasNudgedSession state sessionID then
        state, false
    else
        { state with nudgedSessions = Set.add sessionID state.nudgedSessions }, true

let private selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt
    | NudgeLoop -> Some loopNudgePrompt
    | _ -> None

let decideNudge isReviewActive lookupChildAgent state sessionID snapshot =
    if not (hasNudgedSession state sessionID) || hasStoppedSession state sessionID then
        state, StandDown
    elif snapshot.alreadyNudged then
        // A nudge prompt already trails the last assistant turn in the history:
        // this stop was nudged before (possibly in a prior process), and the
        // agent has not yet produced fresh work. Stand down — no double nudge.
        deleteNudgedSession state sessionID, StandDown
    else
        let state = rememberAgent state sessionID snapshot.agentFromMessage
        let context =
            { todos = snapshot.todos
              lastAssistantMessage = snapshot.lastAssistantMessage
              hasActiveRunner = false
              isLoopActive = isReviewActive sessionID }
        match decide context with
        | NudgeNone
        | NudgeRunner -> deleteNudgedSession state sessionID, StandDown
        | action ->
            match selectNudgePrompt action with
            | None -> deleteNudgedSession state sessionID, StandDown
            | Some promptText ->
                let agentOpt = getAgent state sessionID |> Option.orElse (lookupChildAgent sessionID)
                { state with lastNudgedSession = Some sessionID }, Send(promptText, agentOpt)

let recordSend state sessionID outcome =
    match outcome with
    | Delivered -> deleteNudgedSession state sessionID
    | Aborted -> stopSession state sessionID
    | Busy -> deleteNudgedSession state sessionID
    | Failed -> addRetryPendingSession (deleteNudgedSession state sessionID) sessionID

let tryRecordSend state sessionID outcome : NudgeShellState option =
    if Set.contains sessionID state.nudgedSessions
       || Set.contains sessionID state.stoppedSessions
       || Set.contains sessionID state.retryPendingSessions
       || Map.containsKey sessionID state.sessionAgents
    then Some(recordSend state sessionID outcome)
    else None

let handleSessionNextStepEnded state finish sessionID =
    let state = deleteRetryPendingSession state sessionID
    if finish <> "" && isTerminalAssistantFinish finish then
        tryClaimNudge state sessionID
    else
        state, false

let handleSessionBusy state sessionID =
    let state =
        if state.lastNudgedSession <> Some sessionID then deleteNudgedSession state sessionID else state
    { state with lastNudgedSession = None }

/// Exhaustive transition over NudgeHostEvent. Each event's outcome is already
/// resolved into a finite constructor at the host boundary, so the kernel only
/// maps a legal state to its successor — no boolean re-interpretation here.
/// `SessionNextPrompted` is inlined (P42) so every transition lives in one fold.
let handleEvent (state: NudgeShellState) (sessionID: string) (event: NudgeHostEvent) : NudgeShellState * bool =
    match event with
    | StreamAbort -> clearSession state sessionID, false
    | SessionDeleted -> clearSession state sessionID, false
    | SessionNextPrompted promptText ->
        (if isNudgePrompt promptText then state else resumeSession state sessionID), false
    | SessionNextRetried -> addRetryPendingSession state sessionID, false
    | MessageUpdated UpdateAborted -> stopSession state sessionID, false
    | MessageUpdated UpdateCompletedAssistant -> tryClaimNudge state sessionID
    | MessageUpdated UpdateNoChange -> state, false
    | MessagePartUpdated PartRetry -> addRetryPendingSession state sessionID, false
    | MessagePartUpdated PartAborted -> stopSession state sessionID, false
    | MessagePartUpdated PartRetryProgress -> deleteRetryPendingSession state sessionID, false
    | MessagePartUpdated PartOther -> state, false
    | SessionNextStepFailed StepFailAbort -> stopSession state sessionID, false
    | SessionNextStepFailed StepFailOther -> state, false
    | SessionNextToolFailed ToolFailAbort -> stopSession state sessionID, false
    | SessionNextToolFailed ToolFailOther -> deleteRetryPendingSession state sessionID, false
    | SessionNextStepEnded finish -> handleSessionNextStepEnded state finish sessionID
    | SessionIdle -> tryClaimNudge state sessionID
    | SessionError SessionErrorAbort -> stopSession state sessionID, false
    | SessionError SessionErrorOther -> addRetryPendingSession state sessionID, false
    | SessionStatusIdle -> tryClaimNudge state sessionID
    | SessionStatusBusy -> handleSessionBusy state sessionID, false
    | SessionStatusRetry -> addRetryPendingSession state sessionID, false
    | RetryProgress -> deleteRetryPendingSession state sessionID, false
    | Other -> state, false

let private sessionEventTypes =
    Set.ofList [
        "session.created"
        "session.updated"
        "session.deleted"
        "session.delete"
        "session.close"
        "session.remove"
    ]

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if Set.contains eventType sessionEventTypes then
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

let isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false

let decodeTodos (todosData: obj) : string list =
    if Dyn.isArray todosData then
        (todosData :?> obj array)
        |> Array.choose (fun todo ->
            let status = Dyn.str todo "status"
            match todoStatusOfString status with
            | Some s when isTerminal s -> None
            | _ -> Some status)
        |> Array.toList
    else []

/// Returns the last completed assistant message's (text, agent) plus whether a
/// nudge prompt already trails it in the history. The trailing-nudge flag is the
/// restart-safe per-stop de-dup signal: if the most recent assistant turn was
/// already followed by a nudge prompt, the stop has been nudged and the agent
/// has not yet produced fresh work, so we must not nudge again.
let decodeLastAssistant (messagesData: obj) : string * string option * bool =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array
        let lastAssistantIdx =
            messagesArr
            |> Array.tryFindIndexBack (fun msg -> isCompletedAssistantMessage (Dyn.get msg "info"))
        match lastAssistantIdx with
        | Some idx ->
            let msg = messagesArr.[idx]
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"
            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
            let text = getPartsText (Dyn.get msg "parts")
            let alreadyNudged =
                messagesArr.[idx + 1 ..]
                |> Array.exists (fun m -> isNudgePrompt (getPartsText (Dyn.get m "parts")))
            text, agent, alreadyNudged
        | None -> "", None, false
    else "", None, false