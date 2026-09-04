namespace Wanxiangshu.Execution.Session.Wait

open System
open System.Collections.Generic
open Wanxiangshu.Foundation

/// DSL-MUTABLE: resource — process-local diagnostic wait registry.
/// It is not a business truth source, recovery input, dedupe key,
/// workflow branch input, or Journal projection.
type CausalWaitRegistry(?historyCapacity: int) =
    let capacity =
        match historyCapacity with
        | Some n when n > 0 -> n
        | _ -> 256

    let gate = obj ()
    // DSL-MUTABLE: resource — active diagnostic wait leases by local id
    let active = Dictionary<int64, DiagnosticWait>()
    // DSL-MUTABLE: resource — bounded diagnostic transition ring buffer
    let history = Queue<WaitTransition>()
    // DSL-MUTABLE: resource — monotonic local lease / transition sequence
    let mutable nextId = 0L
    // DSL-MUTABLE: resource — snapshot sequence observed by diagnostics
    let mutable snapshotSequence = 0L

    let pushHistory (transition: WaitTransition) =
        history.Enqueue transition

        while history.Count > capacity do
            history.Dequeue() |> ignore

    let leave (leaseId: int64) (exit: DiagnosticWaitExit) =
        lock gate (fun () ->
            match active.TryGetValue leaseId with
            | false, _ -> ()
            | true, wait ->
                active.Remove leaseId |> ignore
                nextId <- nextId + 1L
                snapshotSequence <- snapshotSequence + 1L

                pushHistory
                    { Sequence = nextId
                      Kind = WaitTransitionKind.Left
                      Wait = wait
                      Exit = Some exit })

    member _.HistoryCapacity = capacity

    interface IWaitObserver with
        member _.Enter(wait: DiagnosticWait) : IWaitLease =
            let leaseId =
                lock gate (fun () ->
                    nextId <- nextId + 1L
                    snapshotSequence <- snapshotSequence + 1L
                    let id = nextId
                    active.[id] <- wait

                    pushHistory
                        { Sequence = id
                          Kind = WaitTransitionKind.Entered
                          Wait = wait
                          Exit = None }

                    id)

            // DSL-MUTABLE: resource — one-shot exit marker for the lease
            let mutable exitMarked: DiagnosticWaitExit option = None
            // DSL-MUTABLE: resource — dispose latch
            let mutable disposed = false

            { new IWaitLease with
                member _.MarkExit(exit: DiagnosticWaitExit) =
                    if not disposed then
                        exitMarked <- Some exit

                member _.Dispose() =
                    if not disposed then
                        disposed <- true
                        leave leaseId (defaultArg exitMarked DiagnosticWaitExit.WaitDisposed) }

    interface IWaitSnapshotReader with
        member _.Snapshot() : DiagnosticWaitSnapshot =
            lock gate (fun () ->
                { Active = active.Values |> Seq.toList
                  History = history |> Seq.toList
                  Sequence = snapshotSequence })

type private PublishingWaitLease(inner: IWaitLease, publish: unit -> unit) =
    interface IWaitLease with
        member _.MarkExit(exit) = inner.MarkExit exit

        member _.Dispose() =
            inner.Dispose()
            publish ()

/// One registry plus its write-only business capability. The diagnostic target
/// is first-bind: a later plugin instance cannot redirect process diagnostics.
type CausalWaitRuntime(?historyCapacity: int) =
    let registry = CausalWaitRegistry(?historyCapacity = historyCapacity)
    let reader = registry :> IWaitSnapshotReader
    let inner = registry :> IWaitObserver
    let targetGate = obj ()
    // DSL-MUTABLE: resource — first-bound process diagnostic target
    let mutable diagnosticTarget: IWaitDiagnosticSink option = None

    let publish () =
        let target = lock targetGate (fun () -> diagnosticTarget)

        target
        |> Option.iter (fun sink ->
            try
                sink.Publish(reader.Snapshot())
            with _ ->
                ())

    let observer: IWaitObserver =
        { new IWaitObserver with
            member _.Enter(wait) =
                let lease = inner.Enter wait
                publish ()
                new PublishingWaitLease(lease, publish) :> IWaitLease }

    member _.BindDiagnosticTarget(target: IWaitDiagnosticSink) =
        let bound =
            lock targetGate (fun () ->
                match diagnosticTarget with
                | Some _ -> false
                | None ->
                    diagnosticTarget <- Some target
                    true)

        if bound then
            publish ()

        bound

    member _.Observer = observer
    member _.SnapshotReader = reader
    member _.HistoryCapacity = registry.HistoryCapacity

module CausalWaitProcess =
    let private processLocal = CausalWaitRuntime()
    let local () = processLocal
