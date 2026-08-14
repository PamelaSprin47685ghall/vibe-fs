namespace Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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

/// Process-local default registry used by production wiring and diagnostics.
/// Safe to lose on crash; never authoritative.
module CausalWaitHub =

    // DSL-MUTABLE: resource — process-local diagnostic wait registry singleton
    let private registry = CausalWaitRegistry()

    // DSL-MUTABLE: resource — workspace path for Scheme B diagnostic file bridge
    let mutable private workspaceDirectory: string option = None

    let reader: IWaitSnapshotReader = registry :> IWaitSnapshotReader

    let private refreshBridge () =
        match workspaceDirectory with
        | Some workspace -> CausalWaitBridge.writeSnapshot workspace reader
        | None -> ()

    /// Observer that refreshes the Scheme B snapshot file after each transition.
    let observer: IWaitObserver =
        let inner = registry :> IWaitObserver

        { new IWaitObserver with
            member _.Enter(wait) =
                let lease = inner.Enter wait
                refreshBridge ()

                { new IWaitLease with
                    member _.MarkExit(exit) = lease.MarkExit exit

                    member _.Dispose() =
                        lease.Dispose()
                        refreshBridge () } }

    let snapshot () = reader.Snapshot()

    let frontiers () = CausalFrontier.ofSnapshot (snapshot ())

    /// Plugin boot sets the workspace so Enter/Leave can overwrite the bridge file.
    let setWorkspace (directory: string option) =
        workspaceDirectory <- directory
        refreshBridge ()

    /// Explicit best-effort write (boot / tests). Never throws into business flow.
    let writeToWorkspace () = refreshBridge ()
