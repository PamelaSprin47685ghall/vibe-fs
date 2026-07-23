module Wanxiangshu.Tests.PtyReadThrottleTests

open Fable.Core
open Wanxiangshu.Kernel.ToolPolling.PtyReadPolicy
open Wanxiangshu.Runtime.ToolSequenceThrottle
open Wanxiangshu.Tests.Assert

/// A sleeper that records delays instead of sleeping.
type RecordingSleeper() =
    let mutable _sleeps: int list = []

    member _.Sleeps = List.rev _sleeps

    interface ISleeper with
        member _.Sleep(ms: int) : JS.Promise<unit> =
            _sleeps <- ms :: _sleeps
            Promise.lift ()

/// Helper to create a terminal id option.
let private tid (id: string) = Some id

let firstReadNoDelay () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "first read: no sleep recorded" (List.isEmpty slp.Sleeps)
        // Verify second consecutive same-PTY read IS delayed
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "second read: sleep recorded" (slp.Sleeps.Length = 1)
        check "second read: delay is 10000ms" (slp.Sleeps.Head = 10000)
    }

let differentTerminalsNoDelay () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        do! throttle.BeforeExecution("s1", "pty_read", tid "B")
        check "different terminals: no delay" (List.isEmpty slp.Sleeps)
    }

let interleavedToolResets () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        do! throttle.BeforeExecution("s1", "executor", None)
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "interleaved tool: no delay" (List.isEmpty slp.Sleeps)
    }

let differentSessionsIsolated () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        do! throttle.BeforeExecution("s2", "pty_read", tid "A")
        check "different sessions: no delay" (List.isEmpty slp.Sleeps)
    }

let missingIdNoDelay () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", None)
        do! throttle.BeforeExecution("s1", "pty_read", None)
        check "missing id: no delay" (List.isEmpty slp.Sleeps)
    }

let sessionCleanupResets () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        throttle.ForgetSession("s1")
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "after cleanup: no delay" (List.isEmpty slp.Sleeps)
    }

let thirdReadAlsoDelayed () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "third read: two delays" (slp.Sleeps.Length = 2)
        check "third read: both 10000ms" (slp.Sleeps |> List.forall (fun d -> d = 10000))
    }

let ptyWriteNoDelay () =
    let slp = RecordingSleeper()
    let throttle = ToolSequenceThrottle(slp)

    promise {
        do! throttle.BeforeExecution("s1", "pty_write", tid "A")
        do! throttle.BeforeExecution("s1", "pty_read", tid "A")
        check "write then read: no delay" (List.isEmpty slp.Sleeps)
    }

let run () : JS.Promise<unit> =
    promise {
        do! firstReadNoDelay ()
        do! differentTerminalsNoDelay ()
        do! interleavedToolResets ()
        do! differentSessionsIsolated ()
        do! missingIdNoDelay ()
        do! sessionCleanupResets ()
        do! thirdReadAlsoDelayed ()
        do! ptyWriteNoDelay ()
    }
