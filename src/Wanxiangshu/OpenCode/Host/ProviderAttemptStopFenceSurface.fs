namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// provider-attempt-recovery-022 fence: opaque handle surface.
///
/// `fence` is an opaque handle: a JS test obtains it, passes it back, and never
/// inspects it (js-semantic-surface-005). Session ids and provider runs cross as
/// plain strings; the fence itself stays a class with private maps.
module ProviderAttemptStopFenceSurface =

    let create () : ProviderAttemptStopFence = ProviderAttemptStopFence.create ()

    let observe (fence: ProviderAttemptStopFence) (sessionId: string) (providerRun: string) : unit =
        fence.Observe(SessionId.create sessionId, ProviderRunIdentity.create providerRun)

    let revoke (fence: ProviderAttemptStopFence) (sessionId: string) : unit =
        fence.Revoke(SessionId.create sessionId)

    let awaitStop (fence: ProviderAttemptStopFence) (sessionId: string) (providerRun: string) : Task<bool> =
        fence.AwaitStop(SessionId.create sessionId, ProviderRunIdentity.create providerRun)

    let snapshot (fence: ProviderAttemptStopFence) : obj =
        let value = fence.Snapshot()

        box
            {| stopped = value.Stopped
               waiting = value.Waiting
               denied = value.Denied |}
