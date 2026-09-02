namespace Wanxiangshu.OpenCode

/// JS-native observation surface for the chat.params binding barrier.
/// The hook mutates only the approved temperature field; provider identity is
/// validated against the session execution binding and never inferred.
module ChatParamsSurface =
    val apply: input: obj -> output: obj -> obj
