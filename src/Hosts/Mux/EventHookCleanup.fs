module Wanxiangshu.Hosts.Mux.EventHookCleanup

open Fable.Core
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionPendingEvidence

/// Single domain command invoked when the host fires
/// session.deleted / session.delete / session.remove / session.close for
/// a Mux workspace.  Tears down every per-session side-effect in one
/// place: the subsession event store append, the actor registry
/// removal, the per-session dispatch mailbox (best-effort until the
/// Mux per-session registry lands), and any buffered evidence under
/// any turn epoch up to a small upper bound.
let handleSessionClosed (directory: string) (workspaceId: string) : JS.Promise<unit> =
    promise {
        let sid = SessionId.create workspaceId
        let eventStore = Wanxiangshu.Runtime.SubsessionEventStore.create directory
        do! eventStore.Append(sid, [ PhysicalSessionClosed sid ]) |> Promise.map ignore
        SubsessionActorRegistry.ClearPoison directory workspaceId
        SubsessionActorRegistry.Remove directory workspaceId

        // S-07 fix: also tear down the per-session dispatch mailbox.
        // Mux does not own its own mailbox yet (Phase 7: capacity
        // downgrade to a single in-flight per physical session), so
        // NotifySessionClosed is a best-effort no-op until the Mux
        // per-session registry is wired in.
        try
            let ws = Id.workspaceIdQuick ("mux:" + workspaceId)

            Wanxiangshu.Runtime.Dispatch.DispatchRegistryInstance.sharedDispatchRegistry.NotifySessionClosed
                ws
                workspaceId
        with _ ->
            ()

        // Phase 8: also forget any evidence still buffered under
        // (workspaceId, _).  The turn-epoch form is keyed by an int we
        // do not know here, so we sweep every epoch from 0 to a small
        // upper bound.  In practice sessions rarely exceed a handful
        // of turns before delete, so 32 is enough.
        for epoch in 0..32 do
            SubsessionPendingEvidence.Forget workspaceId epoch
    }
