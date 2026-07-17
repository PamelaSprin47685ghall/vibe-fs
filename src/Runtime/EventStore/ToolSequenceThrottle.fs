module Wanxiangshu.Runtime.ToolSequenceThrottle

open Fable.Core
open Wanxiangshu.Kernel.ToolPolling.PtyReadPolicy

/// Sleeper abstraction — injectable for testability.
type ISleeper =
    abstract Sleep: milliseconds: int -> JS.Promise<unit>

/// Production sleeper using Promise.sleep.
type PromiseSleeper() =
    interface ISleeper with
        member _.Sleep(ms: int) : JS.Promise<unit> = Promise.sleep ms

/// Per-session last tool invocation tracker. Observes the tool call sequence
/// and decides whether the current pty_read should be delayed.
type ToolSequenceThrottle(?sleeper: ISleeper) =
    let sleeper = defaultArg sleeper (PromiseSleeper())
    let mutable lastBySession: Map<string, LastToolInvocation> = Map.empty

    /// Observe a tool invocation and decide whether to delay.
    /// Returns immediately (no sleep) if RunImmediately.
    /// If DelayBeforeRun, sleeps the requested duration before returning.
    member _.BeforeExecution(sessionID: string, toolName: string, terminalId: string option) : JS.Promise<unit> =
        promise {
            // Atomically: read previous, compute decision, write current
            let previous = Map.tryFind sessionID lastBySession

            let decision = decide previous toolName terminalId

            // Write current before releasing the lock
            lastBySession <-
                Map.add
                    sessionID
                    { ToolName = toolName
                      TerminalId = terminalId }
                    lastBySession

            match decision with
            | RunImmediately -> ()
            | DelayBeforeRun ms -> do! sleeper.Sleep ms
        }

    /// Remove a session's state (e.g. on session close).
    member _.ForgetSession(sessionID: string) : unit =
        lastBySession <- Map.remove sessionID lastBySession

    /// Reset all state (for testing cleanup).
    member _.Reset() : unit = lastBySession <- Map.empty
