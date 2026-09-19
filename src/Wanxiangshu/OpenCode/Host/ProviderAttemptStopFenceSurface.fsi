namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// provider-attempt-recovery-022 fence: opaque handle surface.
///
/// `fence` is an opaque handle: a JS test obtains it, passes it back, and never
/// inspects it (js-semantic-surface-005). Session ids and provider runs cross as
/// plain strings; the fence itself stays a class with private maps.
module ProviderAttemptStopFenceSurface =
    val create: unit -> ProviderAttemptStopFence

    val observe: fence: ProviderAttemptStopFence -> sessionId: string -> providerRun: string -> unit

    val revoke: fence: ProviderAttemptStopFence -> sessionId: string -> unit

    val awaitStop: fence: ProviderAttemptStopFence -> sessionId: string -> providerRun: string -> Task<bool>

    val snapshot: fence: ProviderAttemptStopFence -> obj
