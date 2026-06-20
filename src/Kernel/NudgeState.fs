module VibeFs.Kernel.NudgeState

open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.Prompts

type NudgeShellState =
    { nudgedSessions: Set<string>
      stoppedSessions: Set<string>
      retryPendingSessions: Set<string>
      sessionAgents: Map<string, string>
      lastNudgedSession: string option
      deliveredCounts: Map<string, int> }

type SessionSnapshot =
    { todos: string list
      lastAssistantMessage: string
      messageCount: int option
      agentFromMessage: string option }

type NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option * messageCount: int option

type SendOutcome =
    | Delivered of messageCount: int option
    | Aborted
    | Busy
    | Failed

/// Decoded host event ready for exhaustive nudge state transition. Host adapters
/// decode their native event shapes into this DU; NudgeState owns the transitions.
type NudgeHostEvent =
    | StreamAbort
    | SessionDeleted
    | SessionNextPrompted of promptText: string
    | SessionNextRetried
    | MessageUpdated of isAbortError: bool * isCompletedAssistant: bool
    | MessagePartUpdated of partType: string * isAbortError: bool * isAbortState: bool
    | SessionNextStepFailed of isAbortError: bool
    | SessionNextToolFailed of isAbortError: bool
    | SessionNextStepEnded of finish: string
    | SessionIdle
    | SessionError of isAbortError: bool
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
      lastNudgedSession = None
      deliveredCounts = Map.empty }

let private hasStoppedSession state sessionID = Set.contains sessionID state.stoppedSessions
let private hasRetryPendingSession state sessionID = Set.contains sessionID state.retryPendingSessions
let private hasNudgedSession state sessionID = Set.contains sessionID state.nudgedSessions
let private getAgent state sessionID = Map.tryFind sessionID state.sessionAgents
let private getDeliveredCount state sessionID = Map.tryFind sessionID state.deliveredCounts

let resumeSession state sessionID =
    { state with
        nudgedSessions = Set.remove sessionID state.nudgedSessions
        retryPendingSessions = Set.remove sessionID state.retryPendingSessions
        stoppedSessions = Set.remove sessionID state.stoppedSessions
        deliveredCounts = Map.remove sessionID state.deliveredCounts
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
    { next with
        sessionAgents = Map.remove sessionID next.sessionAgents
        deliveredCounts = Map.remove sessionID next.deliveredCounts }

let private setDeliveredCount state sessionID count =
    { state with deliveredCounts = Map.add sessionID count state.deliveredCounts }

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
                  isLoopActive = isReviewActive sessionID }
            match decide context with
            | NudgeNone
            | NudgeRunner -> deleteNudgedSession state sessionID, StandDown
            | action ->
                match selectNudgePrompt action with
                | None -> deleteNudgedSession state sessionID, StandDown
                | Some promptText ->
                    let agentOpt = getAgent state sessionID |> Option.orElse (lookupChildAgent sessionID)
                    { state with lastNudgedSession = Some sessionID }, Send(promptText, agentOpt, snapshot.messageCount)

let recordSend state sessionID outcome =
    match outcome with
    | Delivered count ->
        let state =
            match count with
            | Some deliveredCount -> setDeliveredCount state sessionID deliveredCount
            | None -> state
        deleteNudgedSession state sessionID
    | Aborted -> stopSession state sessionID
    | Busy -> deleteNudgedSession state sessionID
    | Failed -> addRetryPendingSession (deleteNudgedSession state sessionID) sessionID

let tryRecordSend state sessionID outcome : NudgeShellState option =
    if Set.contains sessionID state.nudgedSessions
       || Set.contains sessionID state.stoppedSessions
       || Set.contains sessionID state.retryPendingSessions
       || Map.containsKey sessionID state.sessionAgents
       || Map.containsKey sessionID state.deliveredCounts
    then Some(recordSend state sessionID outcome)
    else None

let handleSessionNextPrompted state promptText sessionID =
    if isNudgePrompt promptText then state else resumeSession state sessionID

let handleMessageUpdated state isAbortError isCompletedAssistantMessage errorValue info sessionID =
    if isAbortError errorValue then
        stopSession state sessionID, false
    elif isCompletedAssistantMessage info then
        tryClaimNudge state sessionID
    else
        state, false

let handleMessagePartUpdated state isAbortError partType errorValue stateValue sessionID =
    if partType = "retry" then
        addRetryPendingSession state sessionID
    elif isAbortError errorValue || isAbortError stateValue then
        stopSession state sessionID
    elif isRetryProgressPart partType then
        deleteRetryPendingSession state sessionID
    else
        state

let handleSessionNextStepFailed state isAbortError errorValue sessionID =
    if isAbortError errorValue then stopSession state sessionID else state

let handleSessionNextToolFailed state isAbortError errorValue sessionID =
    if isAbortError errorValue then stopSession state sessionID else deleteRetryPendingSession state sessionID

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

let handleSessionError state isAbortError errorValue sessionID =
    if isAbortError errorValue then stopSession state sessionID else addRetryPendingSession state sessionID

/// Exhaustive transition over NudgeHostEvent. Replaces the host-layer string
/// match: the compiler now enforces that every event kind is handled.
let handleEvent (state: NudgeShellState) (sessionID: string) (event: NudgeHostEvent) : NudgeShellState * bool =
    match event with
    | StreamAbort -> clearSession state sessionID, false
    | SessionDeleted -> clearSession state sessionID, false
    | SessionNextPrompted promptText -> handleSessionNextPrompted state promptText sessionID, false
    | SessionNextRetried -> addRetryPendingSession state sessionID, false
    | MessageUpdated(isAbortError, isCompletedAssistant) ->
        if isAbortError then stopSession state sessionID, false
        elif isCompletedAssistant then tryClaimNudge state sessionID
        else state, false
    | MessagePartUpdated(partType, isAbortError, isAbortState) ->
        if partType = "retry" then addRetryPendingSession state sessionID, false
        elif isAbortError || isAbortState then stopSession state sessionID, false
        elif isRetryProgressPart partType then deleteRetryPendingSession state sessionID, false
        else state, false
    | SessionNextStepFailed isAbortError ->
        if isAbortError then stopSession state sessionID, false else state, false
    | SessionNextToolFailed isAbortError ->
        if isAbortError then stopSession state sessionID, false else deleteRetryPendingSession state sessionID, false
    | SessionNextStepEnded finish -> handleSessionNextStepEnded state finish sessionID
    | SessionIdle -> tryClaimNudge state sessionID
    | SessionError isAbortError ->
        if isAbortError then stopSession state sessionID, false else addRetryPendingSession state sessionID, false
    | SessionStatusIdle -> tryClaimNudge state sessionID
    | SessionStatusBusy -> handleSessionBusy state sessionID, false
    | SessionStatusRetry -> addRetryPendingSession state sessionID, false
    | RetryProgress -> deleteRetryPendingSession state sessionID, false
    | Other -> state, false

let getSessionID (eventType: string) (props: obj) : string =
    let part = Dyn.get props "part"
    let info = Dyn.get props "info"
    let candidates =
        [ Dyn.str props "sessionID"
          Dyn.str part "sessionID"
          Dyn.str info "sessionID"
          if eventType = "session.created" || eventType = "session.updated" || eventType = "session.deleted" || eventType = "session.delete" || eventType = "session.close" || eventType = "session.remove" then
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

let decodeLastAssistant (messagesData: obj) : string * string option * int option =
    if Dyn.isArray messagesData then
        let messagesArr = messagesData :?> obj array
        let messageCount = Some messagesArr.Length
        let lastAssistant =
            messagesArr
            |> Array.tryFindBack (fun msg -> isCompletedAssistantMessage (Dyn.get msg "info"))
        match lastAssistant with
        | Some msg ->
            let info = Dyn.get msg "info"
            let agentVal = Dyn.get info "agent"
            let agent = if Dyn.isNullish agentVal then None else Some (string agentVal)
            let text = getPartsText (Dyn.get msg "parts")
            text, agent, messageCount
        | None -> "", None, messageCount
    else "", None, None