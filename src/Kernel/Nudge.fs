module VibeFs.Kernel.Nudge

open System.Text.RegularExpressions
open VibeFs.Kernel.Prompts

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

/// Detect whether position is inside a markdown code fence (``` or ~~~).
let private isInsideCodeFence (text: string) : bool =
    let lines = text.Split('\n')
    let mutable fenceCount = 0
    let mutable inFence = false
    for line in lines do
        let trimmed = line.TrimStart()
        if trimmed.StartsWith("```") || trimmed.StartsWith("~~~") then
            fenceCount <- fenceCount + 1
            inFence <- fenceCount % 2 = 1
    inFence

let private isQuestion (text: string) =
    not (isInsideCodeFence text) && questionRe.IsMatch text
let private skipsTodo (text: string) =
    not (isInsideCodeFence text) && skipTodoRe.IsMatch text
let private skipsLoop (text: string) =
    not (isInsideCodeFence text) && skipLoopRe.IsMatch text

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

let retryProgressEvents: Set<string> =
    Set.ofList
        [ "session.next.step.started"; "session.next.step.ended"
          "session.next.text.started"; "session.next.text.delta"; "session.next.text.ended"
          "session.next.reasoning.started"; "session.next.reasoning.delta"; "session.next.reasoning.ended"
          "session.next.tool.input.started"; "session.next.tool.input.delta"; "session.next.tool.input.ended"
          "session.next.tool.called"; "session.next.tool.progress"; "session.next.tool.success" ]

let retryProgressParts: Set<string> =
    Set.ofList
        [ "step-start"; "step-finish"; "text"; "reasoning"; "tool"; "agent"
          "subtask"; "file"; "snapshot"; "patch" ]

let isRetryProgressEvent (eventType: string) : bool = Set.contains eventType retryProgressEvents
let isRetryProgressPart (partType: string) : bool = Set.contains partType retryProgressParts

let isTerminalAssistantFinish (finish: string) : bool =
    let normalized = finish.ToLower().Replace("-", "").Replace("_", "").Replace(" ", "")
    not (normalized.Contains("tool")) && not (normalized.Contains("abort"))

let isNudgePrompt (text: string) : bool = text = todoNudgePrompt || text = loopNudgePrompt

let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a -> box {| agent = a; parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}
