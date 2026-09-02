namespace Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module EventStore =
    val createLocal: commonDir: string -> writerId: string -> integrator: ICanonicalIntegrator -> IEventStore
