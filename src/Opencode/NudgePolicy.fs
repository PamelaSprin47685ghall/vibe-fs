module VibeFs.Opencode.NudgePolicy

open System
open VibeFs.Kernel
open VibeFs.Kernel.DomainError
open VibeFs.Kernel.JsBoundary
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeEvents
open VibeFs.Kernel.Prompts

// ── Dyn-boundary utility functions (stateless, no side effects) ──

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

let isAbortDomainError (error: obj) : bool =
    match translateJsError error with
    | MessageAborted -> true
    | _ -> false

// ── Immutable shell state ──

type NudgeShellState =
    { nudgedSessions: Set<string>
      stoppedSessions: Set<string>
      retryPendingSessions: Set<string>
      sessionAgents: Map<string, string>
      lastNudgedSession: string option
      deliveredCounts: Map<string, int> }

let emptyState () =
    { nudgedSessions = Set.empty; stoppedSessions = Set.empty; retryPendingSessions = Set.empty
      sessionAgents = Map.empty; lastNudgedSession = None; deliveredCounts = Map.empty }

let hasStoppedSession state sessionID = Set.contains sessionID state.stoppedSessions
let hasRetryPendingSession state sessionID = Set.contains sessionID state.retryPendingSessions
let hasNudgedSession state sessionID = Set.contains sessionID state.nudgedSessions
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
    { next with sessionAgents = Map.remove sessionID next.sessionAgents; deliveredCounts = Map.remove sessionID next.deliveredCounts }

let private setDeliveredCount state sessionID count =
    { state with deliveredCounts = Map.add sessionID count state.deliveredCounts }

let private addNudgedSession state sessionID = { state with nudgedSessions = Set.add sessionID state.nudgedSessions }
let private deleteNudgedSession state sessionID = { state with nudgedSessions = Set.remove sessionID state.nudgedSessions }
let private addRetryPendingSession state sessionID = { state with retryPendingSessions = Set.add sessionID state.retryPendingSessions }
let private deleteRetryPendingSession state sessionID = { state with retryPendingSessions = Set.remove sessionID state.retryPendingSessions }

// ── Claim & decision ──

let tryClaimNudge (state: NudgeShellState) (sessionID: string) : NudgeShellState * bool =
    if hasStoppedSession state sessionID || hasRetryPendingSession state sessionID || hasNudgedSession state sessionID then
        state, false
    else addNudgedSession state sessionID, true

type NudgeDecision =
    | StandDown
    | Send of promptText: string * agentOpt: string option * messageCount: int option

let private selectNudgePrompt = function
    | NudgeTodo -> Some todoNudgePrompt | NudgeLoop -> Some loopNudgePrompt | _ -> None

let decideNudge (isLoopActive: bool) (state: NudgeShellState) (sessionID: string)
                (todos: string list) (lastAssistantMessage: string) (messageCount: int option)
                (agentFromMessage: string option) : NudgeShellState * NudgeDecision =
    if not (hasNudgedSession state sessionID) || hasStoppedSession state sessionID then state, StandDown
    else
        let state = rememberAgent state sessionID agentFromMessage
        match messageCount with
        | Some count when getDeliveredCount state sessionID = Some count -> deleteNudgedSession state sessionID, StandDown
        | _ ->
            let context = { todos = todos; lastAssistantMessage = lastAssistantMessage; hasActiveRunner = false; isLoopActive = isLoopActive }
            match decide context with
            | NudgeNone | NudgeRunner -> deleteNudgedSession state sessionID, StandDown
            | action ->
                match selectNudgePrompt action with
                | None -> deleteNudgedSession state sessionID, StandDown
                | Some promptText ->
                    let state = { state with lastNudgedSession = Some sessionID }
                    state, Send(promptText, getAgent state sessionID, messageCount)

type SendOutcome = Delivered of messageCount: int option | Aborted | Busy | Failed

let recordSend (state: NudgeShellState) (sessionID: string) (outcome: SendOutcome) : NudgeShellState =
    match outcome with
    | Delivered count -> let s = match count with Some c -> setDeliveredCount state sessionID c | None -> state in deleteNudgedSession s sessionID
    | Aborted -> stopSession state sessionID
    | Busy -> deleteNudgedSession state sessionID
    | Failed -> addRetryPendingSession (deleteNudgedSession state sessionID) sessionID

// ── Event dispatch (pure state machine) ──

let dispatchEvent state eventType (props: obj) sessionID : NudgeShellState * bool =
    match eventType with
    | "stream-abort" -> clearSession state sessionID, false
    | "session.delete" | "session.close" | "session.remove" | "session.deleted" -> clearSession state sessionID, false
    | "session.next.prompted" ->
        let prompt = Dyn.get props "prompt"
        let promptText = Dyn.str prompt "text"
        let text = if promptText <> "" then promptText else getPartsText (Dyn.get props "parts")
        if isNudgePrompt text then state, false else resumeSession state sessionID, false
    | "session.next.retried" -> addRetryPendingSession state sessionID, false
    | "message.updated" ->
        let info = Dyn.get props "info"
        if isAbortDomainError (Dyn.get info "error") then stopSession state sessionID, false
        elif isCompletedAssistantMessage info then tryClaimNudge state sessionID
        else state, false
    | "message.part.updated" ->
        let part = Dyn.get props "part"
        if Dyn.str part "type" = "retry" then addRetryPendingSession state sessionID, false
        elif isAbortDomainError (Dyn.get part "error") || isAbortDomainError (Dyn.get part "state") then stopSession state sessionID, false
        elif isRetryProgressPart (Dyn.str part "type") then deleteRetryPendingSession state sessionID, false
        else state, false
    | "session.next.step.failed" ->
        if isAbortDomainError (Dyn.get props "error") then stopSession state sessionID, false else state, false
    | "session.next.tool.failed" ->
        if isAbortDomainError (Dyn.get props "error") then stopSession state sessionID, false
        else deleteRetryPendingSession state sessionID, false
    | "session.next.step.ended" ->
        let state = deleteRetryPendingSession state sessionID
        let finish = let d = Dyn.str props "finish" in if d <> "" then d else Dyn.str (Dyn.get props "info") "finish"
        if finish <> "" && isTerminalAssistantFinish finish then tryClaimNudge state sessionID else state, false
    | "session.idle" -> tryClaimNudge state sessionID
    | "session.error" ->
        if isAbortDomainError (Dyn.get props "error") then stopSession state sessionID, false
        else addRetryPendingSession state sessionID, false
    | "session.status" ->
        match Dyn.str (Dyn.get props "status") "type" with
        | "idle" -> tryClaimNudge state sessionID
        | "busy" ->
            let s = if state.lastNudgedSession <> Some sessionID then deleteNudgedSession state sessionID else state
            { s with lastNudgedSession = None }, false
        | "retry" -> addRetryPendingSession state sessionID, false
        | _ -> state, false
    | _ ->
        if isRetryProgressEvent eventType then deleteRetryPendingSession state sessionID, false
        else state, false
