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

/// Per-session dedup state: the last non-none action and the assistant message
/// it was fired against.  Suppress only when BOTH repeat — overlapping events
/// for the same idle transition see the same message and are deduped, but when
/// the agent produces new output (or calls tools and stops again) the message
/// changes and a fresh nudge is correctly allowed through.
type SessionNudgeState = { lastAction: NudgeAction option; lastMessage: string }

let freshSession : SessionNudgeState = { lastAction = None; lastMessage = "" }

type CoordinatorState = { sessions: Map<string, SessionNudgeState> }

let freshCoordinator : CoordinatorState = { sessions = Map.empty }

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

/// Terminal todo statuses that should NOT count as open work.
type TodoStatus = Completed | Cancelled | Abandoned | InProgress | Pending

let todoStatusOfString (s: string) : TodoStatus option =
    match s.ToLowerInvariant() with
    | "completed" -> Some Completed
    | "cancelled" -> Some Cancelled
    | "abandoned" -> Some Abandoned
    | "in_progress" | "inprogress" -> Some InProgress
    | "pending" -> Some Pending
    | _ -> None

let isTerminal (s: TodoStatus) : bool =
    match s with
    | Completed | Cancelled | Abandoned -> true
    | InProgress | Pending -> false

/// Set of terminal statuses for backward compatibility with string-based checks.
let terminalTodoStatuses: Set<string> = Set.ofList [ "completed"; "cancelled"; "abandoned" ]

/// The host-facing coordinator: wraps the pure decision in mutable per-session
/// state and a one-shot suppress set.  Effects only through `shouldNudge`.
type NudgeCoordinator() =
    let mutable state = freshCoordinator
    let mutable suppressed = Set.empty<string>

    /// Decide and record the nudge for a session, unless it was suppressed this
    /// round (a suppress consumes itself — a single free pass).
    member _.shouldNudge(sessionId, context: NudgeContext) : string =
        if Set.contains sessionId suppressed then
            suppressed <- Set.remove sessionId suppressed
            "none"
        else
            let next, action = update state sessionId context
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
