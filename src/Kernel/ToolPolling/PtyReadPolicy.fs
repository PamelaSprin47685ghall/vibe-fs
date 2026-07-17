module Wanxiangshu.Kernel.ToolPolling.PtyReadPolicy

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

/// Record of the last tool invocation for PTY throttle purposes.
type LastToolInvocation =
    { ToolName: string
      TerminalId: string option }

/// Decision from the PTY-read throttle policy.
type DelayDecision =
    | RunImmediately
    | DelayBeforeRun of milliseconds: int

/// Pure decision logic: decide whether to delay the current pty_read
/// based on the previous tool invocation.
let decide
    (previous: LastToolInvocation option)
    (currentTool: string)
    (currentTerminalId: string option)
    : DelayDecision =
    match previous with
    | Some p when
        p.ToolName = "pty_read"
        && currentTool = "pty_read"
        && p.TerminalId = currentTerminalId
        && currentTerminalId.IsSome
        ->
        DelayBeforeRun 10_000
    | _ -> RunImmediately
