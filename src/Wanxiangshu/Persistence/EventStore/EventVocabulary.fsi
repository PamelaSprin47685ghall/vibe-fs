namespace Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module ProjectionCutTailEvent =
    [<Literal>]
    val EventType: string = "ProjectionCutTail"

    val streamId: rule: string -> EventStreamId
