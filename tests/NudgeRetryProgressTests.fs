module Wanxiangshu.Tests.NudgeRetryProgressTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.RetryProgress

let isRetryProgressEventTrue () =
    let firstKnown = Set.toList retryProgressEvents |> List.head
    check "retryProgressEvent true for first known" (isRetryProgressEvent firstKnown)

let isRetryProgressEventFalse () =
    check "retryProgressEvent false for unknown" (not (isRetryProgressEvent "not-an-event"))

let isRetryProgressPartTrue () =
    let firstKnown = Set.toList retryProgressParts |> List.head
    check "retryProgressPart true for first known" (isRetryProgressPart firstKnown)

let isRetryProgressPartFalse () =
    check "retryProgressPart false for unknown" (not (isRetryProgressPart "unknown-part"))

let run () : unit =
    isRetryProgressEventTrue ()
    isRetryProgressEventFalse ()
    isRetryProgressPartTrue ()
    isRetryProgressPartFalse ()
