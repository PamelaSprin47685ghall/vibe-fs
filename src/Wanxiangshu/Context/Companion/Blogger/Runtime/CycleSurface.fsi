namespace Wanxiangshu.Context.Companion.Blogger.Runtime

/// Blogger cycle effect surface. Materialization and receipt identity remain
/// projection-owned; JS observes only counts and explicit rejection text.
[<RequireQualifiedAccess>]
module BloggerCycleSurface =
    val scenario: actions: obj array -> obj
