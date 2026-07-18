module Wanxiangshu.Kernel.Dispatch.Events

open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types

/// Persistent event log for the dispatch lifecycle. Replaces the previously
/// scattered `EventLogRuntime.append*` calls with one schema-versioned shape.
type DispatchEvent =
    | DispatchRequested of DispatchIdentity * requestKind: string * promptDigest: string
    | DispatchTransportStarted of DispatchId * atMs: int64
    | DispatchHostAccepted of DispatchId * acceptance: DispatchAcceptance * atMs: int64
    | DispatchRunObserved of DispatchId * hostUserMessageId: string * atMs: int64
    | DispatchTerminal of DispatchId * terminal: DispatchTerminal * atMs: int64
    | DispatchLateReceipt of DispatchId * reason: string * atMs: int64

module DispatchEvent =
    /// Stable kind strings written into NDJSON. Do not rename without bumping
    /// schema version; old logs must remain readable.
    let requestKindStr = "dispatch_requested"
    let transportStartedKindStr = "dispatch_transport_started"
    let hostAcceptedKindStr = "dispatch_host_accepted"
    let runObservedKindStr = "dispatch_run_observed"
    let terminalKindStr = "dispatch_terminal"
    let lateReceiptKindStr = "dispatch_late_receipt"

    let toString (e: DispatchEvent) : string =
        match e with
        | DispatchRequested _ -> requestKindStr
        | DispatchTransportStarted _ -> transportStartedKindStr
        | DispatchHostAccepted _ -> hostAcceptedKindStr
        | DispatchRunObserved _ -> runObservedKindStr
        | DispatchTerminal _ -> terminalKindStr
        | DispatchLateReceipt _ -> lateReceiptKindStr
