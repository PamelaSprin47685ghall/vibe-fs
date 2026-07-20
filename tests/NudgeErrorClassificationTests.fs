module Wanxiangshu.Tests.NudgeErrorClassificationTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.MuxLogicalReceipt

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

let sendOutcome_hasAcceptanceUnknownVariant () =
    let _ = SendOutcome.AcceptanceUnknown "err"
    chk "AcceptanceUnknown variant exists" true

let leaseStatus_hasAcceptanceUnknownVariant () =
    let _ = LeaseStatus.AcceptanceUnknown
    chk "LeaseStatus.AcceptanceUnknown exists" true

let continuationOutcome_hasUnknownTerminals () =
    let _ = ContinuationOutcome.AcceptanceUnknown
    let _ = ContinuationOutcome.AbortUnknown
    chk "ContinuationOutcome unknown terminals exist" true

let nudgeOutcome_hasAbortUnknownVariant () =
    let _ = NudgeOutcome.AbortUnknown
    chk "NudgeOutcome.AbortUnknown exists" true

let muxBooleanTrueIsAcceptanceUnknown () =
    match classify (box true) "s1" "d1" "d1" with
    | AcceptanceUnknown msg -> chk "boolean true is AcceptanceUnknown" (msg.Contains("nudge resolved true"))
    | other -> failwith ("expected AcceptanceUnknown, got " + string other)

let muxBooleanFalseIsBusy () =
    match classify (box false) "s1" "d1" "d1" with
    | Busy -> chk "boolean false is Busy" true
    | other -> failwith ("expected Busy, got " + string other)

let muxValidReceiptIsAccepted () =
    let receipt =
        createObj
            [ "messageId", box "m1"
              "sessionId", box "s1"
              "dispatchId", box "d1" ]

    match classify receipt "s1" "d1" "d1" with
    | Accepted _ -> chk "valid receipt Accepted" true
    | other -> failwith ("expected Accepted, got " + string other)

let isAcceptanceUnknownMessageRecognizesPrefix () =
    chk "prefix" (isAcceptanceUnknownMessage "AcceptanceUnknown: foo")
    chk "abort prefix" (isAbortUnavailableMessage "AbortUnavailable: bar")

let run () =
    sendOutcome_hasNotNeededVariant ()
    sendOutcome_hasSnapshotUnavailableVariant ()
    sendOutcome_hasClaimConflictVariant ()
    sendOutcome_hasEventStoreFailureVariant ()
    sendOutcome_hasAcceptanceUnknownVariant ()
    leaseStatus_hasAcceptanceUnknownVariant ()
    continuationOutcome_hasUnknownTerminals ()
    nudgeOutcome_hasAbortUnknownVariant ()
    muxBooleanTrueIsAcceptanceUnknown ()
    muxBooleanFalseIsBusy ()
    muxValidReceiptIsAccepted ()
    isAcceptanceUnknownMessageRecognizesPrefix ()
