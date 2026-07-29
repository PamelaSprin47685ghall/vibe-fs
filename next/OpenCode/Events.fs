namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity

open Wanxiangshu.Next.Kernel

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of reason: string
    | Failed of error: string

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

/// One provider run's contribution to session-wide A (HOST-005, COMPANION-005).
///
/// Segmented by `ProviderRun` rather than kept as a flat `string list`. COMPANION-005
/// makes "this round's assistant body" the unit that is appended to B, and a flat
/// list cannot answer which round a chunk belongs to. It also made re-recording
/// unsafe: a reconcile pass that observed the same run twice appended its text
/// twice, and de-duplicating by text collapsed two runs that genuinely said the
/// same thing.
type SessionARecord =
    { ProviderRun: ProviderRunIdentity
      Text: string }

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool
    abstract IsCompleted: sessionId: SessionId -> bool

    /// HOST-005: A material for one provider run.
    ///
    /// On the interface, not on one concrete port. The writer used to live only on
    /// `HostEventPort`, so `TerminalSessionA` reached it through a type test and
    /// silently discarded A for every other port — which is why the reader needed a
    /// current-turn fallback that masked the loss.
    abstract RecordSessionA: sessionId: SessionId -> providerRun: ProviderRunIdentity -> text: string -> unit

    abstract SessionARecords: sessionId: SessionId -> SessionARecord list

module Events =

    /// Per-session A segments, keyed by provider run.
    ///
    /// Recording the same run again REPLACES its segment. A run's parts become
    /// visible progressively, so the last observation is the complete one;
    /// appending would duplicate the prefix on every reconcile pass.
    type private SessionAccumulator() =
        let bySession = Dictionary<SessionId, ResizeArray<SessionARecord>>()
        let gate = obj ()

        member _.Record (sessionId: SessionId) (providerRun: ProviderRunIdentity) (text: string) =
            if not (String.IsNullOrWhiteSpace text) then
                lock gate (fun () ->
                    let segments =
                        match bySession.TryGetValue sessionId with
                        | true, existing -> existing
                        | false, _ ->
                            let created = ResizeArray<SessionARecord>()
                            bySession.[sessionId] <- created
                            created

                    let record =
                        { ProviderRun = providerRun
                          Text = text }

                    match segments.FindIndex(fun segment -> segment.ProviderRun = providerRun) with
                    | -1 -> segments.Add record
                    | index -> segments.[index] <- record)

        member _.Records(sessionId: SessionId) =
            lock gate (fun () ->
                match bySession.TryGetValue sessionId with
                | true, segments -> segments |> Seq.toList
                | false, _ -> [])

    /// The one event port.
    ///
    /// `DeterministicEventPort` used to sit beside this with an identical
    /// implementation — same listener list, same accumulator, and a `NotifyTerminal`
    /// that computed the same answer by a different spelling. It had no production
    /// consumer at all; only tests constructed it, so it was a second definition of
    /// this class kept alive by its test callers.
    type HostEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let accumulator = SessionAccumulator()
        let lockObj = obj ()

        let notify sessionId outcome =
            let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

            for handler in handlers do
                handler sessionId outcome

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
                let hasListeners = lock lockObj (fun () -> listeners.Count > 0)
                notify sessionId outcome
                hasListeners

            member _.IsCompleted sessionId = false

            member _.RecordSessionA sessionId providerRun text =
                accumulator.Record sessionId providerRun text

            member _.SessionARecords sessionId = accumulator.Records sessionId
