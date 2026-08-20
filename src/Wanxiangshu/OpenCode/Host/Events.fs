namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

open Wanxiangshu.Foundation

type TerminalStop =
    { Reason: string
      AuthorityRootUserMessageId: AuthorityRootUserMessageId option }

[<RequireQualifiedAccess>]
module TerminalStop =
    let session reason =
        { Reason = reason
          AuthorityRootUserMessageId = None }

    let forAuthority authorityRoot reason =
        { Reason = reason
          AuthorityRootUserMessageId = Some authorityRoot }

    let belongsTo authorityRoot stop =
        stop.AuthorityRootUserMessageId = Some authorityRoot

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of stop: TerminalStop
    | Failed of stop: TerminalStop

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract SubscribeFutureTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool

module Events =

    /// The one event port.
    ///
    /// `DeterministicEventPort` used to sit beside this with an identical
    /// implementation — same listener list, same accumulator, and a `NotifyTerminal`
    /// that computed the same answer by a different spelling. It had no production
    /// consumer at all; only tests constructed it, so it was a second definition of
    /// this class kept alive by its test callers.
    /// One registration slot. A bare function element would be curry-wrapped by
    /// Fable at every call site (the wrapper identity is re-created per call),
    /// so a reference-based Remove could never match and disposal leaked the
    /// listener. A record keeps one stable identity; disposal is a flag flip.
    /// DSL-state-combination: physical — listener identity + live flag for disposal without identity leak
    type ListenerRegistration =
        { Listener: TerminalCompletionListener
          mutable Live: bool }

    type HostEventPort() as this =
        // DSL-MUTABLE: resource — listener registry
        let listeners = ResizeArray<ListenerRegistration>()
        let lockObj = obj ()
        // DSL-MUTABLE: resource — last completed run cache per session
        let lastCompletedRun = System.Collections.Generic.Dictionary<string, string>()

        /// Last non-duplicate terminal per session. ARCH-002: sticky stores an
        /// already-derived TerminalOutcome for wake/delivery reliability only —
        /// not a second derivation of business facts. Late SubscribeTerminalListener
        /// replays so InstallRun after Notify never loses completion.
        ///
        /// Cap 256 sessions by insert order: duplicate writes update the value
        /// without re-enqueue so the queue cannot grow unbounded on churn.
        // DSL-MUTABLE: resource — sticky terminal registry for late subscriber replay
        let stickyTerminal = Dictionary<string, TerminalOutcome>()
        let stickyOrder = Queue<string>()
        let stickyCap = 256

        let notifyRegistration sessionId outcome registration =
            if registration.Live then
                registration.Listener sessionId outcome

        let trimSticky () =
            while stickyOrder.Count > stickyCap do
                let evicted = stickyOrder.Dequeue()
                stickyTerminal.Remove(evicted) |> ignore

        let rememberSticky key outcome =
            lock lockObj (fun () ->
                let isNew = not (stickyTerminal.ContainsKey key)
                stickyTerminal.[key] <- outcome

                if isNew then
                    stickyOrder.Enqueue key

                if isNew then
                    trimSticky ()

                listeners.Count > 0
                && listeners |> Seq.exists (fun registration -> registration.Live))

        let notify sessionId outcome =
            let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

            for registration in handlers do
                notifyRegistration sessionId outcome registration

        let replayStickyTerminals listener =
            let stickyReplay =
                lock lockObj (fun () -> stickyTerminal |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.toList)

            for sessionKey, outcome in stickyReplay do
                listener (SessionId.create sessionKey) outcome

        let subscribe replaySticky listener =
            let registration: ListenerRegistration =
                lock lockObj (fun () ->
                    let registration: ListenerRegistration = { Listener = listener; Live = true }

                    listeners.Add registration
                    registration)

            if replaySticky then
                replayStickyTerminals listener

            { new IDisposable with
                member _.Dispose() =
                    lock lockObj (fun () -> registration.Live <- false) }

        /// One Completed terminal per (session, provider run), across every plugin
        /// instance sharing this port.
        ///
        /// HOST-012 loads the plugin once per directory; the root and the worktree
        /// instances both reconcile a child session's terminal and both call
        /// NotifyTerminal on the shared port. The run's token guard absorbs the
        /// duplicate when no new run subscribed in between — but the review nudge
        /// installs a fresh run inside that window, and the stale duplicate then
        /// completes it with the PREVIOUS run's outcome (measured: the challenge
        /// nudge's run completed before the reviewer answered, so the Orchestrator's
        /// second await returned early and a confirmed review read as UNPROVEN).
        let isCompletedRunDuplicate sessionId (providerRun: ProviderRunIdentity) =
            let key = SessionId.value sessionId
            let runValue = ProviderRunIdentity.value providerRun

            lock lockObj (fun () ->
                match lastCompletedRun.TryGetValue key with
                | true, last when last = runValue -> true
                | _ ->
                    lastCompletedRun.[key] <- runValue
                    false)

        member private _.IsCompletedDuplicate(sessionId: SessionId, outcome: TerminalOutcome) =
            match outcome with
            | TerminalOutcome.Completed result when not (isNull (box result.ProviderRun)) ->
                isCompletedRunDuplicate sessionId result.ProviderRun
            | _ -> false

        /// Terminal completions arrive through NotifyTerminal from the reconcile
        /// path. Raw host event observation is handled upstream by the signal stack
        /// (HostSignalAdapter / HostSignalSubscribe).
        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) = subscribe true listener

            member _.SubscribeFutureTerminalListener(listener) = subscribe false listener

            member _.NotifyTerminal sessionId outcome =
                if not (this.IsCompletedDuplicate(sessionId, outcome)) then
                    let key = SessionId.value sessionId
                    let hasListeners = rememberSticky key outcome
                    notify sessionId outcome
                    hasListeners
                else
                    false
