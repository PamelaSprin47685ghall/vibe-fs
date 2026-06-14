module VibeFs.Kernel.Nudge

open System.Text.RegularExpressions

/// Which kind of nudge, if any, a session needs right now.
type NudgeAction = NudgeTodo | NudgeLoop | NudgeRunner | NudgeNone

let ofString = function
    | "nudge-todo" -> Ok NudgeTodo
    | "nudge-loop" -> Ok NudgeLoop
    | "nudge-runner" -> Ok NudgeRunner
    | "none" -> Ok NudgeNone
    | other -> Error $"Invalid NudgeAction: \"{other}\""

let toString = function
    | NudgeTodo -> "nudge-todo" | NudgeLoop -> "nudge-loop"
    | NudgeRunner -> "nudge-runner" | NudgeNone -> "none"

/// Everything `decideNudge` needs to make a pure decision.
type NudgeContext =
    { todos: string list
      lastAssistantMessage: string
      hasActiveRunner: bool
      isLoopActive: bool }

let skipTodoRe = Regex(@"<skip-todo-check\s*\/?>", RegexOptions.IgnoreCase)
let skipLoopRe = Regex(@"<skip-loop-check\s*\/?>", RegexOptions.IgnoreCase)
let questionRe = Regex(@"\?\s*$")

let private isQuestion (text: string) = questionRe.IsMatch text
let private skipsTodo (text: string) = skipTodoRe.IsMatch text
let private skipsLoop (text: string) = skipLoopRe.IsMatch text

/// Priority: open todos → active runner → active loop → none.  A question or
/// explicit skip tag suppresses the todo/loop nudges so the agent can wait for
/// the user's answer.
let decide (context: NudgeContext) : NudgeAction =
    let text = context.lastAssistantMessage.Trim()
    if not context.todos.IsEmpty then
        if skipsTodo text || isQuestion text then NudgeNone else NudgeTodo
    elif context.hasActiveRunner then NudgeRunner
    elif context.isLoopActive then
        if skipsLoop text || isQuestion text then NudgeNone else NudgeLoop
    else NudgeNone

/// Suppress when the message is a question, carries a skip tag, or would repeat
/// the previous non-none action (avoid nagging).
let shouldSuppress (context: NudgeContext) (previous: NudgeAction option) : bool =
    let text = context.lastAssistantMessage.Trim()
    if isQuestion text then true
    elif skipsTodo text || skipsLoop text then true
    else
        match previous with
        | Some prev when prev <> NudgeNone -> decide context = prev
        | _ -> false

/// Per-session throttle state.  Each action tracks its own last-fired timestamp.
type SessionNudgeState =
    { todoAt: int; loopAt: int; runnerAt: int; lastIndex: int option }

let freshSession : SessionNudgeState =
    { todoAt = 0; loopAt = 0; runnerAt = 0; lastIndex = None }

let timestampKeyFor = function
    | NudgeTodo | NudgeNone -> "todoAt"
    | NudgeLoop -> "loopAt"
    | NudgeRunner -> "runnerAt"

let private stamp (state: SessionNudgeState) key now : SessionNudgeState =
    let nextIndex = state.lastIndex |> Option.map ((+) 1) |> Option.defaultValue 0
    match key with
    | "todoAt" -> { state with todoAt = now; lastIndex = Some nextIndex }
    | "loopAt" -> { state with loopAt = now; lastIndex = Some nextIndex }
    | _ -> { state with runnerAt = now; lastIndex = Some nextIndex }

type CoordinatorState = { sessions: Map<string, SessionNudgeState> }

let freshCoordinator : CoordinatorState = { sessions = Map.empty }

/// Pure state transition: decide the action, record its timestamp, return the
/// updated coordinator.  NudgeNone leaves the state untouched.
let update (state: CoordinatorState) (sessionId: string) (context: NudgeContext) (now: int)
           : CoordinatorState * NudgeAction =
    let action = decide context
    if action = NudgeNone then state, NudgeNone
    else
        let prev = Map.tryFind sessionId state.sessions |> Option.defaultValue freshSession
        let key = timestampKeyFor action
        let updated = stamp prev key now
        { sessions = Map.add sessionId updated state.sessions }, action

/// Terminal todo statuses that should NOT count as open work.
let terminalTodoStatuses: Set<string> = Set.ofList [ "completed"; "cancelled"; "abandoned" ]

/// The host-facing coordinator: wraps the pure decision in mutable per-session
/// state and a one-shot suppress set.  Effects only through `shouldNudge`.
type NudgeCoordinator() =
    let mutable state = freshCoordinator
    let mutable suppressed = Set.empty<string>

    /// Decide and record the nudge for a session, unless it was suppressed this
    /// round (a suppress consumes itself — a single free pass).
    member _.shouldNudge(sessionId, context: NudgeContext, now) : string =
        if Set.contains sessionId suppressed then
            suppressed <- Set.remove sessionId suppressed
            "none"
        else
            let next, action = update state sessionId context now
            state <- next
            toString action

    /// Grant `sessionId` a one-shot suppression.
    member _.suppress(sessionId) = suppressed <- Set.add sessionId suppressed

    /// Forget a session's nudge history and any pending suppression.
    member _.clearSession(sessionId) =
        state <- { state with sessions = Map.remove sessionId state.sessions }
        suppressed <- Set.remove sessionId suppressed

    /// Reset the whole coordinator.
    member _.clear() =
        state <- freshCoordinator
        suppressed <- Set.empty

let defaultCoordinator = NudgeCoordinator()

