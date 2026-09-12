namespace Wanxiangshu.Persistence.EventStore

/// Additive authoritative event vocabulary. Unknown durable facts fail closed
/// before they reach the canonical Integrator.
[<RequireQualifiedAccess>]
module ProjectionCutTailEvent =
    [<Literal>]
    let EventType = "ProjectionCutTail"

    let streamId rule =
        EventStreamId.create ("integrator/cut-tail/" + rule)
