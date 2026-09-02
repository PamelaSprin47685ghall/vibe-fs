namespace Wanxiangshu.Context.Companion

open System.Threading.Tasks

/// Context-compression runtime owner. One opaque PluginRuntimeScope owns the
/// physical Blogger park/flight/drain resources; companion recovery waiters and
/// material offers use the same owner boundary.
[<RequireQualifiedAccess>]
module CompanionRuntimeSurface =

    val main: value: obj -> obj
    val squash: value: obj -> obj
    val toml: value: obj -> string

    /// Isolate physical shared-flight state before a semantic runtime test.
    val createScope: unit -> obj

    val dispose: scope: obj -> unit
    val park: scope: obj -> sessionId: string -> Task<obj>
    val cancelParked: scope: obj -> sessionId: string -> unit
    val hasParked: scope: obj -> sessionId: string -> bool
    val offerMaterial: scope: obj -> sessionId: string -> context: obj -> string
    val consumeStaged: scope: obj -> sessionId: string -> obj
    val claimCurrentRequest: scope: obj -> sessionId: string -> context: obj -> string
    val acquireMaterialization: scope: obj -> sessionId: string -> Task<obj>
    val releaseMaterialization: lease: obj -> unit
    val releaseCurrentRequest: scope: obj -> sessionId: string -> requestId: string -> string
    val hasFlight: scope: obj -> sessionId: string -> bool
    val currentRequest: scope: obj -> sessionId: string -> obj
    val scope: unit -> obj
    val setPendingOffer: scope: obj -> sessionId: string -> context: obj -> string
    val offerParked: scope: obj -> sessionId: string -> context: obj -> string
    val tryGetFlight: scope: obj -> sessionId: string -> obj
    val peekCurrentRequest: scope: obj -> sessionId: string -> obj
    val openDrain: root: string -> obj
    val closedDrain: unit -> obj
    val setDrainWindow: scope: obj -> sessionId: string -> window: obj -> unit
    val isDrainOpen: scope: obj -> sessionId: string -> bool
    val sealRuntime: scope: obj -> sessionId: string -> unit
    val blocksNewRequest: durableSealed: bool -> hasFlightValue: bool -> drainOpenValue: bool -> bool

    val decideMaterial:
        hasOpenProducerValue: bool -> hasParkedValue: bool -> hasFlightValue: bool -> context: obj -> string

    val createCompanion: sessionId: string -> obj
