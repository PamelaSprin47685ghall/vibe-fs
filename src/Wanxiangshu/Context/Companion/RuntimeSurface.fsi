namespace Wanxiangshu.Context.Companion

open System.Threading.Tasks

/// Context-compression runtime owner. One opaque IBloggerRuntimeHost owns the
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
    val offerMaterial: scope: obj -> sessionId: string -> context: obj -> string
    val claimCurrentRequest: scope: obj -> sessionId: string -> context: obj -> string
    val claimFlight: scope: obj -> sessionId: string -> context: obj -> obj
    val acquireMaterialization: scope: obj -> sessionId: string -> Task<obj>
    val releaseMaterialization: lease: obj -> unit
    val releaseCurrentRequest: scope: obj -> sessionId: string -> requestId: string -> string
    val beginBloggerShutdown: scope: obj -> unit

    val claimRepairEpisode:
        scope: obj ->
        requestId: string ->
        authorityRoot: string ->
        mainSessionId: string ->
        bloggerSessionId: string ->
            string

    val drainRepairEpisodes: scope: obj -> Task
    val currentRequest: scope: obj -> sessionId: string -> obj
    val scope: unit -> obj
    val setPendingOffer: scope: obj -> sessionId: string -> context: obj -> string
    val offerParked: scope: obj -> sessionId: string -> context: obj -> string
    val tryGetFlight: scope: obj -> sessionId: string -> obj
    val peekCurrentRequest: scope: obj -> sessionId: string -> obj
    val createCompanion: sessionId: string -> obj
