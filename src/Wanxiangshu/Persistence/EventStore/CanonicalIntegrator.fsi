namespace Wanxiangshu.Persistence.EventStore

/// The canonical durable-event integrator. It exposes only factory creation;
/// all rule registration, builder and internal state remain private to the
/// durable-convergence owner.
[<RequireQualifiedAccess>]
module CanonicalIntegrator =
    val create: unit -> ICanonicalIntegrator
