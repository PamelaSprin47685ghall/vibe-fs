namespace Wanxiangshu.Execution.Session

open System
open System.Collections.Generic
open Fable.Core
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.TerminalSignal

module private TerminalRegistryInternal =

    /// One registration. A record keeps one stable identity, so disposal
    /// unlinks exactly that registration and a late notification snapshot
    /// skips it. `Session` restricts the registration to one session; `None`
    /// is the process-wide registration the legacy event port exposes.
    type Registration =
        { Session: SessionId option
          Listener: TerminalCompletionListener
          mutable Live: bool }

    /// The fault channel of the listener dispatch. The pre-migration event port
    /// reported a faulting listener on `console.error`, so the host log keeps
    /// seeing it; a `Debug.WriteLine` would have stayed inside the process.
    [<Emit("console.error($0, $1)")>]
    let logError (prefix: string) (message: string) : unit = jsNative

open TerminalRegistryInternal

/// The product's single holder of terminal facts and terminal subscriptions.
///
/// HNI A2 (owner ruling, 2026-10-22): the neutral host ports carry no
/// per-session terminal subscription, so the product owns it. This type is the
/// one place that holds terminal facts, the sticky cache and the listener list;
/// the OpenCode adapter's event port and its injected session port only
/// forward to it, so there is no second copy of the sticky cache or of the
/// listener registry.
///
/// The facts are the existing exact ones: a `TerminalOutcome` with its
/// `AgentRunResult` (and therefore its exact `ProviderRunIdentity`) or its
/// `TerminalStop` (and therefore its exact `AuthorityRootUserMessageId`). The
/// registry invents no authority field of its own and applies no new matching
/// law — the subscriber keeps the law it already has
/// (`Execution/Delegation/Fork/Host/RunLifecycle.fs` `replayedTerminalAddressesRun`:
/// `Completed` needs the same child and the same authority root, `Failed` needs
/// the exact authority root, `Aborted` is observation only).
///
/// A run that is already closed is not addressed by a late terminal: the
/// subscriber disposes its subscription when the run closes (the existing
/// `claimPendingRun` path), and a disposed subscription is never delivered to
/// again — the disposal marks the registration dead before unlinking it, so an
/// in-flight notification snapshot also skips it.
///
/// The two subscription kinds keep their existing difference (host-boundary
/// event-port clause): a sticky subscriber is answered by the last terminal
/// this registry has proven for its session, a future-only subscriber is never
/// replayed an existing sticky terminal and only observes what arrives after
/// it subscribed.
type TerminalRegistry() as this =

    // DSL-MUTABLE: resource — terminal listener registry
    let listeners = ResizeArray<Registration>()
    let lockObj = obj ()

    // DSL-MUTABLE: resource — last completed run per session (idempotence)
    let lastCompletedRun = Dictionary<string, string>()

    /// The last proven terminal per session, for sticky replay.
    ///
    /// The 256-session cap by insert order is the pre-migration behaviour: the
    /// unbounded ancestor was deliberately bounded by commit `747b3235b`
    /// ("Events: stickyTerminal capped at 256 sessions by insert order"), and
    /// the migration carried that cap over unchanged rather than inventing one.
    // DSL-MUTABLE: resource — sticky terminal registry for late subscriber replay
    let stickyTerminal = Dictionary<string, TerminalOutcome>()
    let stickyOrder = Queue<string>()
    let stickyCap = 256

    let trimSticky () =
        while stickyOrder.Count > stickyCap do
            let evicted = stickyOrder.Dequeue()
            stickyTerminal.Remove evicted |> ignore

    let rememberSticky (key: string) (outcome: TerminalOutcome) =
        lock lockObj (fun () ->
            let isNew = not (stickyTerminal.ContainsKey key)
            stickyTerminal.[key] <- outcome

            if isNew then
                stickyOrder.Enqueue key

            if isNew then
                trimSticky ()

            listeners.Count > 0
            && listeners |> Seq.exists (fun registration -> registration.Live))

    /// Whether the registration is addressed by the session's terminal.
    let isAddressed (registration: Registration) (sessionId: SessionId) =
        match registration.Session with
        | None -> true
        | Some wanted -> wanted = sessionId

    /// The delivery itself. A faulting listener must not stop the others.
    let deliverSafely (registration: Registration) (sessionId: SessionId) (outcome: TerminalOutcome) =
        try
            registration.Listener sessionId outcome
        with error ->
            logError
                "TERMINAL-DISPATCH"
                (sprintf "terminal listener faulted for session %s: %s" (SessionId.value sessionId) error.Message)

    /// One delivery. A disposed registration is skipped even when it was in the
    /// notification snapshot, so a closed run is never delivered a terminal.
    let deliver (registration: Registration) (sessionId: SessionId) (outcome: TerminalOutcome) =
        if registration.Live && isAddressed registration sessionId then
            deliverSafely registration sessionId outcome

    let notify (sessionId: SessionId) (outcome: TerminalOutcome) =
        let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

        for registration in handlers do
            deliver registration sessionId outcome

    let replaySticky (session: SessionId option) (listener: TerminalCompletionListener) =
        let replay =
            lock lockObj (fun () ->
                stickyTerminal
                |> Seq.map (fun kv -> kv.Key, kv.Value)
                |> Seq.filter (fun (key, _) ->
                    match session with
                    | None -> true
                    | Some wanted -> key = SessionId.value wanted)
                |> Seq.toList)

        for sessionKey, outcome in replay do
            listener (SessionId.create sessionKey) outcome

    let subscribe
        (session: SessionId option)
        (replayStickyTerminals: bool)
        (listener: TerminalCompletionListener)
        : IDisposable =
        let registration: Registration =
            lock lockObj (fun () ->
                let registration: Registration =
                    { Session = session
                      Listener = listener
                      Live = true }

                listeners.Add registration
                registration)

        if replayStickyTerminals then
            replaySticky session listener

        { new IDisposable with
            member _.Dispose() =
                lock lockObj (fun () ->
                    registration.Live <- false
                    listeners.Remove registration |> ignore) }

    /// One completed terminal per (session, provider run). The plugin is
    /// loaded once per directory, so the root and the worktree instance both
    /// reconcile a child session's terminal; the run's token guard absorbs the
    /// duplicate, and the sticky cache must not be rewritten by it either.
    let isCompletedRunDuplicate (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        let key = SessionId.value sessionId
        let runValue = ProviderRunIdentity.value providerRun

        lock lockObj (fun () ->
            match lastCompletedRun.TryGetValue key with
            | true, last when last = runValue -> true
            | _ ->
                lastCompletedRun.[key] <- runValue
                false)

    /// A terminal fact proven by the product, published to the subscribers.
    /// Returns whether a subscriber was live when the fact landed.
    member this.NotifyTerminal (sessionId: SessionId) (outcome: TerminalOutcome) : bool =
        let duplicate =
            match outcome with
            | TerminalOutcome.Completed result when not (isNull (box result.ProviderRun)) ->
                isCompletedRunDuplicate sessionId result.ProviderRun
            | _ -> false

        if duplicate then
            false
        else
            let hasListeners = rememberSticky (SessionId.value sessionId) outcome
            notify sessionId outcome
            hasListeners

    /// The sticky subscription of one session: the last proven terminal of that
    /// session is replayed, then later terminals are delivered.
    member this.SubscribeTerminal(sessionId: SessionId, listener: TerminalCompletionListener) : IDisposable =
        subscribe (Some sessionId) true listener

    /// The future-only subscription of one session: nothing is replayed; only
    /// terminals that arrive after the subscription are delivered.
    member this.SubscribeFutureTerminal(sessionId: SessionId, listener: TerminalCompletionListener) : IDisposable =
        subscribe (Some sessionId) false listener

    /// Whether the session has a live subscriber. The send path uses it to
    /// refuse a send that no listener will ever answer (the legacy
    /// "AG-LISTENER-BEFORE-SEND" call-order guard).
    member this.HasSubscriber(sessionId: SessionId) : bool =
        lock lockObj (fun () ->
            listeners
            |> Seq.exists (fun registration -> registration.Live && registration.Session = Some sessionId))

    interface ISessionTerminalSource with
        member _.SubscribeTerminalListener(listener) = subscribe None true listener
        member _.SubscribeFutureTerminalListener(listener) = subscribe None false listener
        member _.NotifyTerminal sessionId outcome = this.NotifyTerminal sessionId outcome

        member _.SubscribeTerminal(sessionId, listener) =
            this.SubscribeTerminal(sessionId, listener)

        member _.SubscribeFutureTerminal(sessionId, listener) =
            this.SubscribeFutureTerminal(sessionId, listener)

        member _.HasSubscriber(sessionId) = this.HasSubscriber(sessionId)
