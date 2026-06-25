module VibeFs.Kernel.Nudge.Coordinator

open VibeFs.Kernel.Nudge

/// Per-session dedup state: the last non-none action and the assistant message
/// it was fired against.  Suppress only when BOTH repeat — overlapping events
/// for the same idle transition see the same message and are deduped, but when
/// the agent produces new output (or calls tools and stops again) the message
/// changes and a fresh nudge is correctly allowed through.
type SessionNudgeState = { lastAction: NudgeAction option; lastMessage: string }

let freshSession : SessionNudgeState = { lastAction = None; lastMessage = "" }

type CoordinatorState = { sessions: Map<string, SessionNudgeState> }

let freshCoordinator : CoordinatorState = { sessions = Map.empty }

type CoordinatorRuntimeState =
    { coordinator: CoordinatorState
      suppressedSessions: Set<string> }

let freshCoordinatorRuntime : CoordinatorRuntimeState =
    { coordinator = freshCoordinator
      suppressedSessions = Set.empty }

/// Pure state transition: decide the action; suppress only if the same action
/// would repeat for the same assistant message (concurrent events for one idle
/// transition).  A different message means the agent did new work — allow it.
/// NudgeNone leaves the state untouched.
let update (state: CoordinatorState) (sessionId: string) (context: NudgeContext)
           : CoordinatorState * NudgeAction =
    let action = decide context
    if action = NudgeNone then state, NudgeNone
    else
        let prev = Map.tryFind sessionId state.sessions |> Option.defaultValue freshSession
        match prev.lastAction with
        | Some last when last = action && prev.lastMessage = context.lastAssistantMessage ->
            state, NudgeNone
        | _ ->
            let updated = { lastAction = Some action; lastMessage = context.lastAssistantMessage }
            { sessions = Map.add sessionId updated state.sessions }, action

/// Suppress a stream-end nudge when the assistant already received the same
/// nudge action for the current phase. A cleared context records NudgeNone,
/// which re-allows the next todo/loop nudge after work resumes.
let shouldSuppressNudge (_sessionId: string) (context: NudgeContext) (previousAction: NudgeAction option) : bool =
    let text = context.lastAssistantMessage.Trim()
    if isQuestion text then true
    elif skipsTodo text || skipsLoop text then true
    else
        match previousAction with
        | Some previous when previous <> NudgeNone -> decide context = previous
        | _ -> false

let consumeSuppression (state: CoordinatorRuntimeState) (sessionId: string) : CoordinatorRuntimeState * bool =
    if Set.contains sessionId state.suppressedSessions then
        { state with suppressedSessions = Set.remove sessionId state.suppressedSessions }, true
    else
        state, false

let suppressSession (state: CoordinatorRuntimeState) (sessionId: string) : CoordinatorRuntimeState =
    { state with suppressedSessions = Set.add sessionId state.suppressedSessions }

let clearRuntimeSession (state: CoordinatorRuntimeState) (sessionId: string) : CoordinatorRuntimeState =
    { coordinator = { state.coordinator with sessions = Map.remove sessionId state.coordinator.sessions }
      suppressedSessions = Set.remove sessionId state.suppressedSessions }

let decideRuntimeAction (state: CoordinatorRuntimeState) (sessionId: string) (context: NudgeContext)
    : CoordinatorRuntimeState * string =
    let nextState, wasSuppressed = consumeSuppression state sessionId
    if wasSuppressed then nextState, "none"
    else
        let nextCoordinator, action = update nextState.coordinator sessionId context
        { nextState with coordinator = nextCoordinator }, toString action
