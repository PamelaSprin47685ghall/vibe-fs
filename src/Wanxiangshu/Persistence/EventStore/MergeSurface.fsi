namespace Wanxiangshu.Persistence.EventStore

open Fable.Core

/// Semantic owner for deterministic k-way ordering of writer streams.
[<RequireQualifiedAccess>]
module EventMergeSurface =
    /// Merge named JS-native writer streams. Writer names only break impossible
    /// duplicate ties; causal readiness and EventId determine the order.
    val merge: streams: obj array -> obj
