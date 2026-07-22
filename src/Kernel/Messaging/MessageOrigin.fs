namespace Wanxiangshu.Kernel.Messaging

type MessageOrigin =
    | Human
    | TodoNudge
    | ReviewNudge
    | RunnerNudge
    | CompactionNudge
    | ForceStop
    | FallbackContinuation

module MessageOrigin =
    let toWireString =
        function
        | Human -> "human"
        | TodoNudge -> "todo_nudge"
        | ReviewNudge -> "review_nudge"
        | RunnerNudge -> "runner_nudge"
        | CompactionNudge -> "compaction_nudge"
        | ForceStop -> "force_stop"
        | FallbackContinuation -> "fallback_continuation"

    let tryParse (s: string) : MessageOrigin option =
        if System.String.IsNullOrWhiteSpace s then
            None
        else
            match s.Trim().ToLowerInvariant() with
            | "human" -> Some Human
            | "todo_nudge"
            | "todo" -> Some TodoNudge
            | "review_nudge"
            | "review" -> Some ReviewNudge
            | "runner_nudge"
            | "runner" -> Some RunnerNudge
            | "compaction_nudge"
            | "compaction" -> Some CompactionNudge
            | "force_stop"
            | "force" -> Some ForceStop
            | "fallback_continuation"
            | "continuation" -> Some FallbackContinuation
            | "nudge" -> Some TodoNudge
            | _ -> None

    let isNudge =
        function
        | TodoNudge
        | ReviewNudge
        | RunnerNudge
        | CompactionNudge
        | ForceStop -> true
        | Human
        | FallbackContinuation -> false
