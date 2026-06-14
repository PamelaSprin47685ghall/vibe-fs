module VibeFs.Mux.StreamEnd

/// Map a nudge action string to the prompt text that should be sent.
let selectNudgePrompt (action: string) (todoPrompt: string) (loopPrompt: string) : string option =
    match action with
    | "nudge-todo" -> Some todoPrompt
    | "nudge-loop" -> Some loopPrompt
    | _ -> None

/// Mutable per-workspace bookkeeping for the stream-end event hook.  The event
/// hook is inherently stateful across async host callbacks.
type StreamEndState =
    { stoppedWorkspaces: System.Collections.Generic.HashSet<string>
      retryPendingWorkspaces: System.Collections.Generic.HashSet<string>
      deliveredCounts: System.Collections.Generic.Dictionary<string, int>
      lastNudgeSignature: System.Collections.Generic.Dictionary<string, string> }

let createStreamEndState () : StreamEndState =
    { stoppedWorkspaces = System.Collections.Generic.HashSet<string>()
      retryPendingWorkspaces = System.Collections.Generic.HashSet<string>()
      deliveredCounts = System.Collections.Generic.Dictionary<string, int>()
      lastNudgeSignature = System.Collections.Generic.Dictionary<string, string>() }
