module Wanxiangshu.Tests.NudgeErrorClassificationTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.FallbackKernel.Types

let sendOutcome_hasNotNeededVariant () =
    let _ = SendOutcome.NotNeeded
    chk "NotNeeded variant exists" true

let sendOutcome_hasSnapshotUnavailableVariant () =
    let _ = SendOutcome.SnapshotUnavailable "err"
    chk "SnapshotUnavailable variant exists" true

let sendOutcome_hasClaimConflictVariant () =
    let _ = SendOutcome.ClaimConflict
    chk "ClaimConflict variant exists" true

let sendOutcome_hasEventStoreFailureVariant () =
    let _ = SendOutcome.EventStoreFailure "err"
    chk "EventStoreFailure variant exists" true

let run () =
    sendOutcome_hasNotNeededVariant ()
    sendOutcome_hasSnapshotUnavailableVariant ()
    sendOutcome_hasClaimConflictVariant ()
    sendOutcome_hasEventStoreFailureVariant ()
