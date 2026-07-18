namespace Wanxiangshu.Kernel.Subsession.Types

open Wanxiangshu.Kernel.FallbackKernel.Types

// ── Strong-typed identifiers ──

type RunId = private RunId of string

type TurnId = private TurnId of string

type SessionId = private SessionId of string

type TurnOrdinal = private TurnOrdinal of int

module RunId =
    let create (s: string) : RunId = RunId s
    let value (RunId s) : string = s

    let newId () : RunId =
        RunId("run-" + System.Guid.NewGuid().ToString("N"))

module TurnId =
    let create (s: string) : TurnId =
        if s = "" then
            failwith "TurnId cannot be empty"

        TurnId s

    let value (TurnId s) : string = s

module SessionId =
    let create (s: string) : SessionId =
        if s = "" then
            failwith "SessionId cannot be empty"

        SessionId s

    let value (SessionId s) : string = s

module TurnOrdinal =
    let first: TurnOrdinal = TurnOrdinal 0
    let value (TurnOrdinal n) : int = n
    let next (TurnOrdinal n) : TurnOrdinal = TurnOrdinal(n + 1)

// ── Run final result ──

type RunFailure =
    | NoModelConfigured
    | FallbackExhausted of lastError: ErrorInput
    | RecoveryExhausted of reason: string
    | ProtocolViolation of reason: string
    | InfrastructureFailure of reason: string

type RunResult =
    | Succeeded of output: string
    | Failed of RunFailure
    | Cancelled

// ── Pure fallback policy (explicit selection ADT) ──

type ModelSelectionState =
    | StableAt of modelIndex: int
    | RetryingAt of modelIndex: int * retryCount: int
    | Scanning of candidateIndex: int * originalIndex: int

type FallbackPolicyState =
    { Selection: ModelSelectionState
      FailureCount: int
      ContinueCount: int
      RecoveryCount: int }

// ── Host start receipt ──

type HostStartReceipt =
    | UserMessageObserved of messageId: string
    | HostRunAccepted of runId: string
    | OrderedTurnMarkerObserved

/// Dispatch failure: only HostRejected may skip idle and retry.
type DispatchFailure =
    | HostRejected of ErrorInput
    | HostAcceptanceUnknown of ErrorInput

// ── Turn anchor ──

/// Anchor for slicing transcript to current turn only.
type TurnAnchor =
    | AnchorByUserMessageId of messageId: string
    | AnchorByHostRunId of runId: string
    | AnchorByTurnMarkerOnly
