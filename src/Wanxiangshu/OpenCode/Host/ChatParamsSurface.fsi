namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.Journal

/// JS-native observation surface for the chat.params binding barrier.
/// The hook mutates only the approved temperature field; provider identity is
/// validated against the session execution binding and never inferred.
module ChatParamsSurface =
    /// Observe against a real durable journal so a managed execution's exact
    /// lease is the evidence the barrier validates.
    val applyWith: journal: JournalHandle option -> input: obj -> output: obj -> obj
    val apply: input: obj -> output: obj -> obj
