namespace Wanxiangshu.Execution.Delegation

/// Plain-data owner for the HostTurnObserved durable fact.
/// Typed execution facts and the Agent projection remain behind this boundary.
[<RequireQualifiedAccess>]
module HostTurnObservedSurface =

    /// Encode one plain HostTurnObserved payload to canonical fact bytes.
    val serialize: value: obj -> string

    /// Decode one fact line without exposing the Fact DU.
    val deserialize: line: string -> obj

    /// Fold the observation through the canonical Agent reducer and expose only
    /// whether it created a session projection. HostTurnObserved is an inbox
    /// observation; it must not mutate LinkageProjection by itself.
    val foldNoop: value: obj -> obj

    /// Stable dedupe identity supplied by the observation payload.
    val identityKey: value: obj -> string
