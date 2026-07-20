namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Subsession.Types
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
      mutable ReceiptWaiter: HostReceiptWaiter option
      mutable CancelRequested: bool
      mutable AbortSent: bool
      mutable Terminal: DispatchTerminal option
      CancelToken: System.Threading.CancellationTokenSource
      mutable OnResolve: unit -> unit
      mutable CancelWaiter: (unit -> unit) option }

module DispatchOps =
    let getNowMs () : int64 = int64 (JS.Constructors.Date.now ())

    let digestForPrompt (identity: DispatchIdentity) : string =
        let key = identity.LogicalTurnId + "|" + identity.PhysicalSessionId
        (Wanxiangshu.Runtime.FileSys.sha256HexTruncated key).[..7]

    let resolveRecord (r: DispatchRecord) (terminal: DispatchTerminal) : unit =
        if r.Terminal.IsNone then
            r.Terminal <- Some terminal
            r.Phase <- Terminal terminal

            // A terminal domain result also terminates the transport wait.
            // Without this, CompleteByTurn/FailByTurn can free the slot while
            // an unresolved host promise remains alive forever.
            match r.CancelWaiter with
            | Some cancelWaiter ->
                r.CancelWaiter <- None
                cancelWaiter ()
            | None -> ()

            let outcome = Failed terminal

            match r.Waiter with
            | Some w ->
                r.Waiter <- None
                w outcome
            | None -> ()

            r.ReceiptWaiter
            |> Option.iter (fun w -> HostReceiptWaiter.resolveFromTerminal w terminal)

            r.OnResolve()
        else
            ()

    let acceptRecord
        (r: DispatchRecord)
        (acceptance: DispatchAcceptance)
        (atMs: int64)
        (logger: IDispatchEventLogger)
        : unit =
        if r.Terminal.IsSome then
            ()
        else
            r.Phase <- HostAccepted

            match acceptance with
            | UserMessageAccepted mid -> r.AcceptedMessageId <- mid
            | RunAccepted rid -> r.AcceptedRunId <- rid
            | OpaqueAccepted _ -> ()

            logger.Log(DispatchHostAccepted(r.Identity.DispatchId, acceptance, atMs))

            match r.Waiter with
            | Some w ->
                r.Waiter <- None
                w (Accepted acceptance)
            | None -> ()

            r.ReceiptWaiter
            |> Option.iter (fun w -> HostReceiptWaiter.resolveFromAcceptance w acceptance)

    let observeRun (r: DispatchRecord) (hostUserMessageId: string) (atMs: int64) (logger: IDispatchEventLogger) : unit =
        if r.Terminal.IsSome then
            ()
        else
            if r.AcceptedMessageId = "" then
                r.AcceptedMessageId <- hostUserMessageId

            r.Phase <- RunObserved
            logger.Log(DispatchRunObserved(r.Identity.DispatchId, hostUserMessageId, atMs))

            r.ReceiptWaiter
            |> Option.iter (fun w -> HostReceiptWaiter.resolveFromReceipt w (UserMessageObserved hostUserMessageId))

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
