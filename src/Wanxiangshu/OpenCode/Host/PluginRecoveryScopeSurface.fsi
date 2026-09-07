namespace Wanxiangshu.OpenCode

/// JS-native owner surface for PAR-011 / PAR-020 admitted-plan semantics.
///
/// The scope stays an opaque handle: JS obtains it from `createRecoveryScope`,
/// passes it back, and never inspects it. Pending/bound plans cross as the
/// opaque handles built by `XWireSurface.pendingPlan` /
/// `XWireSurface.bindProviderRun`; observations return as plain JSON views.
/// Every decision — freeze admission, binding, record/peek/consume, ownership —
/// delegates to the real `PluginRecoveryScope` and the `XWireSurface` typed
/// bridges. No plan equality or binding algorithm is copied here.
module PluginRecoveryScopeSurface =

    val createRecoveryScope: unit -> obj

    val freezeAttemptPlan: scope: obj -> sessionId: string -> physical: string -> pending: obj -> obj

    val recordAttemptPlan: scope: obj -> sessionId: string -> physical: string -> pending: obj -> unit

    val bindAttempt: scope: obj -> sessionId: string -> physical: string -> providerRun: string -> obj

    val recordBound: scope: obj -> sessionId: string -> providerRun: string -> bound: obj -> unit

    val peekAttempt: scope: obj -> sessionId: string -> providerRun: string -> obj

    val consumeAttempt: scope: obj -> sessionId: string -> providerRun: string -> obj

    val recoveryOwnership: scope: obj -> obj
