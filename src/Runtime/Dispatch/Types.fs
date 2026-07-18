namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.PromiseQueue

/// Stable, exhaustive return from `Dispatch`.
type DispatchOutcome =
    | Accepted of DispatchAcceptance
    | Failed of DispatchTerminal

/// Host-agnostic event log interface.
type IDispatchEventLogger =
    abstract Log: DispatchEvent -> unit

/// In-memory logger for tests.
type InMemoryDispatchEventLogger() =
    let mutable events: DispatchEvent list = []

    interface IDispatchEventLogger with
        member _.Log(e) = events <- e :: events

    member _.Events = List.rev events

/// One in-flight dispatch per physical session.
type DispatchRecord =
    { Identity: DispatchIdentity
      mutable Phase: DispatchPhase
      mutable AcceptedMessageId: string
      mutable AcceptedRunId: string
      mutable Waiter: (DispatchOutcome -> unit) option
      mutable CancelRequested: bool
      mutable AbortSent: bool
      mutable Terminal: DispatchTerminal option
      CancelToken: System.Threading.CancellationTokenSource }

module DispatchOps =
    let getNowMs () : int64 = int64 (JS.Constructors.Date.now ())

    let digestForPrompt (identity: DispatchIdentity) : string =
        let key = identity.LogicalTurnId + "|" + identity.PhysicalSessionId
        (Wanxiangshu.Runtime.FileSys.sha256HexTruncated key).[..7]

    let resolveRecord (r: DispatchRecord) (terminal: DispatchTerminal) : unit =
        if r.Terminal.IsNone then
            r.Terminal <- Some terminal
            r.Phase <- Terminal terminal
            let outcome = Failed terminal

            match r.Waiter with
            | Some w ->
                r.Waiter <- None
                w outcome
            | None -> ()

    let acceptRecord
        (r: DispatchRecord)
        (acceptance: DispatchAcceptance)
        (atMs: int64)
        (logger: IDispatchEventLogger)
        : unit =
        r.Phase <- HostAccepted

        match acceptance with
        | UserMessageAccepted mid -> r.AcceptedMessageId <- mid
        | RunAccepted rid -> r.AcceptedRunId <- rid
        | OpaqueAccepted _ -> ()

        logger.Log(DispatchHostAccepted(r.Identity.DispatchId, acceptance, atMs))

    let observeRun (r: DispatchRecord) (hostUserMessageId: string) (atMs: int64) (logger: IDispatchEventLogger) : unit =
        if r.AcceptedMessageId = "" then
            r.AcceptedMessageId <- hostUserMessageId

        r.Phase <- RunObserved
        logger.Log(DispatchRunObserved(r.Identity.DispatchId, hostUserMessageId, atMs))

    let rejectUnknown (r: DispatchRecord) (errName: string) (message: string) : unit =
        resolveRecord
            r
            (AcceptanceUnknown
                { ErrorName = errName
                  DomainError = None
                  Message = message
                  StatusCode = None
                  IsRetryable = Some true })

    let isCancelRequested (r: DispatchRecord) (cancellation: System.Threading.CancellationToken) : bool =
        cancellation.IsCancellationRequested || r.CancelRequested
