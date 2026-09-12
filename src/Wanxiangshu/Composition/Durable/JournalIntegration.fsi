namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Persistence.EventStore

/// Registered Journal oracle: one EventEnvelope in, one ProjectionSet out.
[<RequireQualifiedAccess>]
module JournalIntegration =
    val rule: IntegrationRule
