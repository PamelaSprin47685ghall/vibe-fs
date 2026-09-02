namespace Wanxiangshu.Context.Companion

/// Context-owned fold oracle for durable recovery laws.
/// It accepts plain fact/envelope data and returns plain projection summaries;
/// the typed fold and all Fable collections remain inside the production boundary.
[<RequireQualifiedAccess>]
module ContextFoldSurface =

    val fold: envelopes: obj array -> obj

    /// Same fold, but each envelope crosses the canonical line codec first.
    val replay: envelopes: obj array -> obj
