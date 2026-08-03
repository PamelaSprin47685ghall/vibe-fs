namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Kernel.Identity

open Wanxiangshu.Kernel

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of reason: string
    | Failed of error: string

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool

module Events =

    /// The one event port.
    ///
    /// `DeterministicEventPort` used to sit beside this with an identical
    /// implementation — same listener list, same accumulator, and a `NotifyTerminal`
    /// that computed the same answer by a different spelling. It had no production
    /// consumer at all; only tests constructed it, so it was a second definition of
    /// this class kept alive by its test callers.
    type HostEventPort() as this =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let lockObj = obj ()
        let lastCompletedRun = System.Collections.Generic.Dictionary<string, string>()

        let notify sessionId outcome =
            let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

            for handler in handlers do
                handler sessionId outcome

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
        member private _.IsCompletedDuplicate(sessionId: SessionId, outcome: TerminalOutcome) =
            match outcome with
            | TerminalOutcome.Completed result ->
                if isNull (box result.ProviderRun) then
                    false
                else
                    let key = SessionId.value sessionId
                    let runValue = ProviderRunIdentity.value result.ProviderRun

                    lock lockObj (fun () ->
                        match lastCompletedRun.TryGetValue key with
                        | true, last when last = runValue -> true
                        | _ ->
                            lastCompletedRun.[key] <- runValue
                            false)
            | _ -> false

        /// Terminal completions arrive through NotifyTerminal from the reconcile
        /// path. Raw host event observation is handled upstream by the signal stack
        /// (HostSignalAdapter / HostSignalSubscribe), so Observe is a no-op.
        member _.Observe(_rawEvent: obj) = ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                if not (this.IsCompletedDuplicate(sessionId, outcome)) then
                    let hasListeners = lock lockObj (fun () -> listeners.Count > 0)
                    notify sessionId outcome
                    hasListeners
                else
                    false
