namespace Wanxiangshu.Runtime.Dispatch

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity

/// Global registry: keyed by (workspace, physical session id), holds a
/// per-session dispatcher. All access MUST go through the registry.
type DispatchRegistry() =
    let mutable dispatchers: Map<string, SessionDispatcher> = Map.empty

    let keyFor (workspace: WorkspaceId) (physicalSessionId: string) : string =
        Id.workspaceIdValue workspace + "|" + physicalSessionId

    member _.GetOrCreate
        (workspace: WorkspaceId)
        (physicalSessionId: string)
        (logger: IDispatchEventLogger)
        : SessionDispatcher =
        let key = keyFor workspace physicalSessionId

        match Map.tryFind key dispatchers with
        | Some d -> d
        | None ->
            let d = SessionDispatcher(workspace, physicalSessionId, logger)
            dispatchers <- Map.add key d dispatchers
            d

    member _.TryGet (workspace: WorkspaceId) (physicalSessionId: string) : SessionDispatcher option =
        let key = keyFor workspace physicalSessionId
        Map.tryFind key dispatchers

    member _.Remove (workspace: WorkspaceId) (physicalSessionId: string) : unit =
        let key = keyFor workspace physicalSessionId
        dispatchers <- Map.remove key dispatchers

    member _.NotifySessionClosed (workspace: WorkspaceId) (physicalSessionId: string) : unit =
        let key = keyFor workspace physicalSessionId
        let opt: SessionDispatcher option = Map.tryFind key dispatchers

        match opt with
        | Some d ->
            d.OnSessionClosed()
            dispatchers <- Map.remove key dispatchers
        | None -> ()

        HostReceiptWaiterRegistry.removeSession workspace physicalSessionId

[<AutoOpen>]
module DispatchRegistryInstance =
    /// Process-wide singleton.  All dispatchers live here; every call site
    /// MUST use `shared` rather than constructing a new `DispatchRegistry()`.
    let sharedDispatchRegistry = DispatchRegistry()
