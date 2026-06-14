module VibeFs.Kernel.NudgeEvents

open VibeFs.Kernel.Prompts

/// Events signalling ongoing (non-terminal) progress during a retry cycle.
/// Used to suppress premature nudges.
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

/// True when a finish reason is terminal (not a tool call, not an abort).
let isTerminalAssistantFinish (finish: string) : bool =
    let normalized = finish.ToLower().Replace("-", "").Replace("_", "").Replace(" ", "")
    not (normalized.Contains("tool")) && not (normalized.Contains("abort"))

/// Is the given text one of the known nudge prompts?
let isNudgePrompt (text: string) : bool = text = todoNudgePrompt || text = loopNudgePrompt

/// Build the body for nudging a session with an optional agent target.
let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a -> box {| agent = a; parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}
