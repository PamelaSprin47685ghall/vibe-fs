module Wanxiangshu.Hosts.Mux.EventHookCleanup

open Fable.Core
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionPendingEvidence
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.RuntimeScopeForgetSession

/// Single domain command invoked when the host fires
/// session.deleted / session.delete / session.remove / session.close for
/// a Mux workspace.  Tears down every per-session side-effect in one
/// place: the subsession event store append, the actor registry
/// removal, the per-session dispatch mailbox, and all buffered evidence.
let handleSessionClosed (scope: RuntimeScope) (directory: string) (workspaceId: string) : JS.Promise<unit> =
    promise {
        let sid = SessionId.create workspaceId
        let eventStore = Wanxiangshu.Runtime.SubsessionEventStore.create directory
        do! eventStore.Append(sid, [ PhysicalSessionClosed sid ]) |> Promise.map ignore
        SubsessionActorRegistry.ClearPoison directory workspaceId
        SubsessionActorRegistry.Remove directory workspaceId
    }
