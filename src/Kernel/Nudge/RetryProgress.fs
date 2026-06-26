module Wanxiangshu.Kernel.Nudge.RetryProgress

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
