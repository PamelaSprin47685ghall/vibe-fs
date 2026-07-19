namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dispatch

/// Process-scoped registry for `HostReceiptWaiter` values. Separated from the
/// core type/module so the companion type can remain in `HostReceiptWaiter.fs`.
module HostReceiptWaiterRegistry =

    let mutable private waiters = Map.empty<string, HostReceiptWaiter>
    let mutable private pendingCancellations = Set.empty<string>

    let private key (workspace: WorkspaceId) (sessionId: string) (turnId: string) : string =
        (Id.workspaceIdValue workspace) + "/" + sessionId + "/" + turnId

    let private workspaceTurnKey (workspace: WorkspaceId) (turnId: string) : string =
        (Id.workspaceIdValue workspace) + "/" + turnId

    let private markCancelled (w: HostReceiptWaiter) : unit =
        if not w.Completed then
            w.TransportState <- UserCancelled

    /// Return the waiter for a dispatch, if one has been registered.
    let tryFind (workspace: WorkspaceId) (sessionId: string) (turnId: string) : HostReceiptWaiter option =
        Map.tryFind (key workspace sessionId turnId) waiters

    /// Create a waiter and register it under its dispatch key. If a waiter
    /// already exists for the same key the existing waiter is returned so that
    /// a retry does not orphan the original Promise.
    let create (workspace: WorkspaceId) (sessionId: string) (turnId: string) : HostReceiptWaiter =
        let k = key workspace sessionId turnId

        match Map.tryFind k waiters with
        | Some existing -> existing
        | None ->
            let resolveRef = ref (fun (_: Result<HostStartReceipt, DispatchFailure>) -> ())
            let p = Promise.create (fun resolve _ -> resolveRef.Value <- resolve)

            let w =
                { WorkspaceId = workspace
                  PhysicalSessionId = sessionId
                  LogicalTurnId = turnId
                  Promise = p
                  Resolve = (fun r -> resolveRef.Value r)
                  Completed = false
                  TransportState = InFlight }

            waiters <- Map.add k w waiters
            let pendingKey = workspaceTurnKey workspace turnId

            if Set.contains pendingKey pendingCancellations then
                markCancelled w

            w

    /// Attempt to resolve a registered waiter with a concrete host receipt.
    let tryResolve
        (workspace: WorkspaceId)
        (sessionId: string)
        (turnId: string)
        (receipt: HostStartReceipt)
        : ResolveAttemptResult =
        match tryFind workspace sessionId turnId with
        | Some w -> HostReceiptWaiter.resolve w receipt
        | None -> NotFound

    /// Resolve or reject all waiters for a session because the session has closed.
    let removeSession (workspace: WorkspaceId) (sessionId: string) : unit =
        let closed = HostReceiptWaiter.sessionClosedError

        waiters
        |> Map.toSeq
        |> Seq.filter (fun (_, w) ->
            (Id.workspaceIdValue w.WorkspaceId) = (Id.workspaceIdValue workspace)
            && w.PhysicalSessionId = sessionId
            && not w.Completed)
        |> Seq.iter (fun (_, w) -> HostReceiptWaiter.reject w (HostRejected closed) UserCancelled |> ignore)

        waiters <-
            waiters
            |> Map.filter (fun _ w ->
                (Id.workspaceIdValue w.WorkspaceId) <> (Id.workspaceIdValue workspace)
                || w.PhysicalSessionId <> sessionId)

    /// Best-effort cancel for every waiter matching the logical turn in the
    /// workspace. Used by `ISubsessionHost.CancelPendingDispatch` which is not
    /// given the physical session id. This only marks the transport state; a
    /// late host receipt can still resolve the Promise.
    let cancelByTurn (workspace: WorkspaceId) (turnId: string) : unit =
        let pendingKey = workspaceTurnKey workspace turnId
        pendingCancellations <- Set.add pendingKey pendingCancellations

        waiters
        |> Map.toSeq
        |> Seq.filter (fun (_, w) ->
            (Id.workspaceIdValue w.WorkspaceId) = (Id.workspaceIdValue workspace)
            && w.LogicalTurnId = turnId
            && not w.Completed)
        |> Seq.iter (fun (_, w) -> markCancelled w)
