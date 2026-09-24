namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open System.Threading
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ParkWake =
    | MaterialAvailable of BloggerRequestContext
    | Cancelled

[<RequireQualifiedAccess>]
type MaterialOfferDisposition =
    | Delivered
    | Staged

[<RequireQualifiedAccess>]
type BloggerFlightRelease =
    | Released
    | Missing
    | Conflict of BloggerRequestId

/// Parking mailbox + exact flight lease (ENFORCER-047/050/160).
///
/// CurrentRequest ownership = exact physical flight lease (ClaimCurrentRequest / TryGetFlight).
/// PendingOffer = the next Main material staged only while Parked (own mailbox).
type IBloggerFlightLease =
    inherit System.IDisposable
    abstract RequestId: BloggerRequestId

[<RequireQualifiedAccess>]
type BloggerFlightClaim =
    | Claimed of IBloggerFlightLease
    | Refreshed of IBloggerFlightLease
    | Conflict of BloggerRequestId

/// Exact live repair episode identity: one process-local episode per exact
/// request/authority (RequestId + AuthorityRoot + MainSessionId + BloggerSessionId).
type BloggerRepairEpisodeIdentity =
    { RequestId: BloggerRequestId
      AuthorityRoot: AuthorityRootUserMessageId
      MainSessionId: SessionId
      BloggerSessionId: SessionId }

/// Terminal/quiescence ports carried with an idle observation.
/// Root workspace crosses as a pure function port (TryRead shape): no
/// compile-order dependency on its contract and no unboxing.
type BloggerRepairTerminalPorts =
    { Quiescence: Wanxiangshu.OpenCode.ISessionQuiescenceGate
      Context: Wanxiangshu.Composition.Turn.ReconciledTurnContext
      SessionPort: Wanxiangshu.OpenCode.ISessionHostPort
      TryReadRootWorkspace: unit -> string option
      EventPort: Wanxiangshu.OpenCode.IEventObservationPort }

/// Repair observation: exact ProviderRun plus either transform facts or
/// terminal/quiescence ports. No business stage.
[<RequireQualifiedAccess>]
type BloggerRepairObservation =
    | TransformToolFacts of providerRun: ProviderRunIdentity * rawMessages: obj list
    | IdleQuiescentTurn of providerRun: ProviderRunIdentity * ports: BloggerRepairTerminalPorts

/// Per-envelope repair reply. No business stage.
[<RequireQualifiedAccess>]
type BloggerRepairOutcome =
    | NudgeSent of promptKey: PromptKey option
    | AabbSent of promptKey: PromptKey option
    | RepairInjected of obj list
    | PendingRepairWait
    | UnownedIdleIgnored
    | SupersededIgnored
    | AbandonedExhausted
    | Completed

/// One queued repair observation with its reply fence.
type BloggerRepairEnvelope =
    { Observation: BloggerRepairObservation
      Reply: TaskCompletionSource<BloggerRepairOutcome> }

/// FIFO TaskCompletionSource mailbox with one receiver CE and per-envelope
/// replies. Start is one-shot and claims the admission; Post queues even
/// while the workflow is busy; Cancel settles every pending reply and receive;
/// Completion is always observable. The mailbox stores observations/replies only.
type private RepairAdmission =
    | Available
    | Claimed
    | Revoked

/// Receive arm for a revoked rendezvous with a terminal fault: synthesizes
/// the faulted placeholder so `Receive`'s next `let!` sees a real error
/// instead of an empty eviction. Kept at namespace level so each branch
/// stays one level under the pyramid lint.
module private ReceiveTerminal =
    let failure (ex: exn) : Task<BloggerRepairEnvelope option> =
        let faulted =
            TaskCompletionSource<BloggerRepairEnvelope option>(TaskCreationOptions.RunContinuationsAsynchronously)

        faulted.SetException ex
        faulted.Task

    /// Both arms of Revoked at one level — fault carrier or nil.
    let revoked (terminalError: exn option) : Task<BloggerRepairEnvelope option> =
        match terminalError with
        | Some ex -> failure ex
        | None -> Task.FromResult None

/// Refusal dispatch for a revoked rendezvous: lands the recorded terminal
/// failure or, absent evidence, the exhausted-abandon marker. Module-level
/// so the caller's match arm stays at a single level.
module private RendezvousReply =
    let refusing (terminalError: exn option) (reply: TaskCompletionSource<BloggerRepairOutcome>) =
        match terminalError with
        | Some ex -> reply.SetException ex
        | None ->
            AsyncSupport.trySetResult reply BloggerRepairOutcome.AbandonedExhausted
            |> ignore

        reply.Task



type BloggerRepairRendezvous(identity: BloggerRepairEpisodeIdentity, onCompleted: unit -> unit) =
    let gate = obj ()
    // DSL-MUTABLE: resource — FIFO repair inbox, receiver waiters, one-shot workflow latch
    let inbox = System.Collections.Generic.Queue<BloggerRepairEnvelope>()

    let waiters =
        System.Collections.Generic.Queue<TaskCompletionSource<BloggerRepairEnvelope option>>()

    let inFlight =
        System.Collections.Generic.HashSet<TaskCompletionSource<BloggerRepairOutcome>>()

    let completion =
        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    // DSL-MUTABLE: resource — one-shot repair episode admission (Available -> Claimed | Revoked)
    let mutable admission = Available
    // DSL-MUTABLE: resource — terminal settlement failure replayed to late observers
    let mutable terminalError: exn option = None

    member _.Identity: BloggerRepairEpisodeIdentity = identity

    member _.Completion: Task = completion.Task :> Task

    /// Terminal settlement failure observed by the owner episode. Some means
    /// every observer of this episode already received, or will receive, this
    /// exact exception; the owner never settles it as a benign outcome.
    member _.TerminalFailure: exn option = terminalError

    /// Admission currently foreclosed: Cancel/Fail both land here. The owner
    /// distinguishes a no-budget-yet-cancelled slot (reclaimable) from a
    /// settlement-failed slot (must stay because its durable outcome is
    /// unknown) via TerminalFailure.
    member _.IsRevoked: bool = admission = Revoked

    member private _.ClaimWorkflowSlot() : bool =
        lock gate (fun () ->
            match admission with
            | Available ->
                admission <- Claimed
                true
            | Claimed -> false
            | Revoked -> false)

    member private _.AwaitCreated(created: Task) : Task =
        task {
            if not (isNull created) then
                do! created
        }
        :> Task

    member private this.CaptureWorkflowFault(workflow: unit -> Task) : Task<exn option> =
        task {
            try
                do! this.AwaitCreated(workflow ())
                return None
            with ex ->
                return Some ex
        }

    member private _.SettleWorkflowFailure(ex: exn) : unit =
        try
            completion.SetException ex
        with _ ->
            ()

    member private this.SettleWorkflowCompletion(fault: exn option) : unit =
        match fault with
        | None -> AsyncSupport.trySetResult completion () |> ignore
        | Some ex -> this.SettleWorkflowFailure ex

    member private this.LaunchWorkflow(workflow: unit -> Task) : unit =
        task {
            try
                let! fault = this.CaptureWorkflowFault workflow
                this.SettleWorkflowCompletion fault
            finally
                onCompleted ()
        }
        :> Task
        |> ignore

    member this.Start(workflow: unit -> Task) : bool =
        if not (this.ClaimWorkflowSlot()) then
            false
        else
            this.LaunchWorkflow workflow
            true

    member _.Post(observation: BloggerRepairObservation) : Task<BloggerRepairOutcome> =
        let reply =
            TaskCompletionSource<BloggerRepairOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

        let envelope: BloggerRepairEnvelope =
            { Observation = observation
              Reply = reply }

        let waiter, accepted =
            lock gate (fun () ->
                if admission = Revoked then
                    None, false
                elif waiters.Count > 0 then
                    inFlight.Add reply |> ignore
                    Some(waiters.Dequeue()), true
                else
                    inbox.Enqueue envelope
                    None, true)

        match waiter with
        | Some receiver ->
            AsyncSupport.trySetResult receiver (Some envelope) |> ignore
            reply.Task
        | None when not accepted -> RendezvousReply.refusing terminalError reply
        | None -> reply.Task

    member _.Receive() : Task<BloggerRepairEnvelope option> =
        lock gate (fun () ->
            if inbox.Count > 0 then
                let envelope = inbox.Dequeue()
                inFlight.Add envelope.Reply |> ignore
                Task.FromResult(Some envelope)
            elif admission = Revoked then
                ReceiveTerminal.revoked terminalError
            else
                let waiter =
                    TaskCompletionSource<BloggerRepairEnvelope option>(
                        TaskCreationOptions.RunContinuationsAsynchronously
                    )

                waiters.Enqueue waiter
                waiter.Task)

    member _.Resolve(envelope: BloggerRepairEnvelope, outcome: BloggerRepairOutcome) : unit =
        let owned = lock gate (fun () -> inFlight.Remove envelope.Reply)

        if owned then
            AsyncSupport.trySetResult envelope.Reply outcome |> ignore

    member _.Cancel() : unit =
        let pending, outstanding, processing, completeWithoutWorkflow =
            lock gate (fun () ->
                if admission = Revoked then
                    [], [], [], false
                else
                    let wasAvailable = admission = Available
                    admission <- Revoked

                    let pendingReplies =
                        [ while inbox.Count > 0 do
                              yield inbox.Dequeue() ]

                    let pendingReceives =
                        [ while waiters.Count > 0 do
                              yield waiters.Dequeue() ]

                    let processingReplies = inFlight |> Seq.toList
                    inFlight.Clear()
                    pendingReplies, pendingReceives, processingReplies, wasAvailable)

        for envelope in pending do
            AsyncSupport.trySetResult envelope.Reply BloggerRepairOutcome.AbandonedExhausted
            |> ignore

        for waiter in outstanding do
            AsyncSupport.trySetResult waiter None |> ignore

        for reply in processing do
            AsyncSupport.trySetResult reply BloggerRepairOutcome.AbandonedExhausted
            |> ignore

        if completeWithoutWorkflow then
            AsyncSupport.trySetResult completion () |> ignore
            onCompleted ()

    /// Settlement failure is terminal for the whole episode, not one reply:
    /// the durable outcome is unknown, so every pending, waiting, and
    /// in-flight observer — and every later Post — rejects on this exact
    /// exception. Never reports abandonment as committed.
    member _.Fail(ex: exn) : unit =
        let pending, outstanding, processing =
            lock gate (fun () ->
                if admission = Revoked then
                    [], [], []
                else
                    terminalError <- Some ex
                    admission <- Revoked

                    let pendingReplies =
                        [ while inbox.Count > 0 do
                              yield inbox.Dequeue() ]

                    let pendingReceives =
                        [ while waiters.Count > 0 do
                              yield waiters.Dequeue() ]

                    let processingReplies = inFlight |> Seq.toList
                    inFlight.Clear()
                    pendingReplies, pendingReceives, processingReplies)

        let safeFailOne (tcs: TaskCompletionSource<'T>) =
            try
                tcs.SetException ex
            with _ ->
                ()

        for envelope in pending do
            safeFailOne envelope.Reply

        for waiter in outstanding do
            safeFailOne waiter

        for reply in processing do
            safeFailOne reply

/// Material mailbox + physical flight lease (R05/R06).
///
/// Mailbox provides material arrival await (ParkTransform) and staged offer
/// with explicit newest-covers replacement business rule (OfferMaterial).
/// Execution flight is an exact RequestId-scoped lease (ClaimCurrentRequest / ReleaseCurrentRequest).
/// Parent-facing slot peeks and fake drain latches are not part of this boundary.
type IBloggerRuntimeHost =
    abstract Cancellation: CancellationToken
    abstract ParkTransform: string -> Task<ParkWake>
    abstract CancelParked: string -> unit
    abstract TryGetFlight: string -> BloggerRequestContext option
    abstract TryPeekCurrentRequest: string -> BloggerRequestContext option
    abstract ClaimCurrentRequest: string * BloggerRequestContext -> BloggerFlightClaim
    abstract ReleaseCurrentRequest: string * BloggerRequestId -> BloggerFlightRelease
    abstract AcquireMaterialization: string -> Task<BloggerMaterializationLease>
    abstract TryDeliverMaterial: string * BloggerRequestContext -> bool
    abstract OfferMaterial: string * BloggerRequestContext -> MaterialOfferDisposition
    abstract ClaimRepairEpisode: BloggerRepairEpisodeIdentity -> Result<BloggerRepairRendezvous, string>
    abstract TryGetRepairEpisode: BloggerRepairEpisodeIdentity -> BloggerRepairRendezvous option
    abstract CancelRepairEpisode: BloggerRepairEpisodeIdentity -> unit
    abstract DrainRepairEpisodes: unit -> Task

/// One event wait for one Blogger continuation.
type ParkedTransform(sessionId: string) as this =
    let completion =
        TaskCompletionSource<ParkWake>(TaskCreationOptions.RunContinuationsAsynchronously)

    let gate = obj ()
    // DSL-MUTABLE: resource — one-shot transform wait settle action
    let mutable settleAction: (ParkWake -> unit) option =
        Some(fun result -> completion.SetResult result)

    member _.SessionId = sessionId

    member _.Completion: Task<ParkWake> = completion.Task

    member private _.TrySettle(result: ParkWake) =
        let pending =
            lock gate (fun () ->
                match settleAction with
                | Some settle ->
                    settleAction <- None
                    Some settle
                | None -> None)

        match pending with
        | Some settle -> settle result
        | None -> ()

    member _.TryResume(context: BloggerRequestContext) =
        this.TrySettle(ParkWake.MaterialAvailable context)

    member _.TryCancel() = this.TrySettle ParkWake.Cancelled
