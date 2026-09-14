namespace Wanxiangshu.Context.Companion

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority

/// Context-compression runtime owner. One opaque IBloggerRuntimeHost owns the
/// physical Blogger park/flight/drain resources; companion recovery waiters and
/// material offers use the same owner boundary.
[<RequireQualifiedAccess>]
module CompanionRuntimeSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int = int (text value)
    let private int64Value (value: obj) : int64 = int64 (text value)

    let private optionalText (value: obj) : string option =
        if isNullish value then None else Some(text value)

    let private hostOf (value: obj) : IBloggerRuntimeHost = unbox<IBloggerRuntimeHost> value

    let private contextOfJs (value: obj) : BloggerRequestContext =
        match text value?kind with
        | "Squash" ->
            let mainSessionId = SessionId.create (text value?mainSession)
            let bloggerSessionId = SessionId.create (text value?bloggerSession)
            let frameEpoch = FrameEpochId.create (int64Value value?frameEpoch)
            let coveredFrameCount = intValue value?coveredFrameCount

            let frameDigests =
                (if isNullish value?digests then
                     [||]
                 else
                     unbox<string array> value?digests)
                |> Array.toList
                |> List.map BlobDigest.create

            let requestId =
                // Canonical Squash identity when no requestId is carried. An
                // explicit requestId is honored: the caller is the exact owner
                // identity, and epoch refresh under the same requestId must
                // remain an in-place Refreshed claim rather than Conflict.
                let rawId =
                    if isNullish value?requestId then
                        ""
                    else
                        text value?requestId

                if System.String.IsNullOrWhiteSpace rawId then
                    BloggerRequestContext.squashRequestId
                        mainSessionId
                        bloggerSessionId
                        frameEpoch
                        coveredFrameCount
                        frameDigests
                else
                    BloggerRequestId.create rawId

            let candidate: BloggerSquashRequestInput =
                { RequestId = requestId
                  MainSessionId = mainSessionId
                  BloggerSessionId = bloggerSessionId
                  FrameEpochId = frameEpoch
                  CoveredFrameCount = coveredFrameCount
                  FrameDigests = frameDigests
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

            match BloggerRequestMaterial.createSquash candidate with
            | Ok verified -> BloggerRequestContext.Squash verified
            | Error rejection -> invalidArg "context" (sprintf "squash context rejected: %A" rejection)
        | _ ->
            let items =
                match BloggerDeltaItemWire.tryListOfJs value?items with
                | Ok parsed -> parsed
                | Error error -> invalidArg "items" error

            let mainSessionId = SessionId.create (text value?mainSession)
            let bloggerSessionId = SessionId.create (text value?bloggerSession)
            let toml = text value?toml
            let previousIngested = int64Value value?previousIngested
            let nextIngested = int64Value value?nextIngested
            let deltaDigest = BlobDigest.create (HostDigest.sha256Hex toml)

            let requestId =
                // Canonical Main identity when no requestId is carried. An
                // explicit requestId is honored: the caller carries exact
                // request identity, and epoch refresh under the same requestId
                // must stay an in-place Refreshed claim rather than Conflict.
                let rawId =
                    if isNullish value?requestId then
                        ""
                    else
                        text value?requestId

                if System.String.IsNullOrWhiteSpace rawId then
                    BloggerRequestContext.mainRequestId
                        mainSessionId
                        bloggerSessionId
                        deltaDigest
                        previousIngested
                        nextIngested
                else
                    BloggerRequestId.create rawId

            let candidate: BloggerMainRequestInput =
                { RequestId = requestId
                  MainSessionId = mainSessionId
                  BloggerSessionId = bloggerSessionId
                  Items = items
                  Toml = toml
                  PreviousIngestedThroughSequence = previousIngested
                  NextIngestedThroughSequence = nextIngested
                  PreviousCoverableTurnCutoffExclusive = intValue value?previousCutoff
                  NextCoverableTurnCutoffExclusive = intValue value?nextCutoff
                  NextCoveredPrefixDigest = text value?nextDigest
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  DeltaDigest = deltaDigest
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

            match BloggerRequestMaterial.createMain candidate with
            | Ok verified -> BloggerRequestContext.Main verified
            | Error rejection -> invalidArg "context" (sprintf "main context rejected: %A" rejection)

    let private contextToJs (value: BloggerRequestContext) : obj =
        match value with
        | BloggerRequestContext.Main main ->
            box
                {| kind = "Main"
                   requestId = BloggerRequestId.value main.RequestId
                   mainSession = SessionId.value main.MainSessionId
                   bloggerSession = SessionId.value main.BloggerSessionId
                   items = main.Items |> List.map BloggerDeltaItemWire.toJs |> List.toArray
                   toml = main.Toml
                   previousIngested = int main.PreviousIngestedThroughSequence
                   nextIngested = int main.NextIngestedThroughSequence
                   previousCutoff = main.PreviousCoverableTurnCutoffExclusive
                   nextCutoff = main.NextCoverableTurnCutoffExclusive
                   nextDigest = main.NextCoveredPrefixDigest
                   frameEpoch = int (FrameEpochId.value main.FrameEpochId)
                   deltaDigest = BlobDigest.value main.DeltaDigest
                   observedEpoch = int (PrefixEpochId.value main.ObservedPrefixEpochId) |}
        | BloggerRequestContext.Squash squash ->
            box
                {| kind = "Squash"
                   requestId = BloggerRequestId.value squash.RequestId
                   mainSession = SessionId.value squash.MainSessionId
                   bloggerSession = SessionId.value squash.BloggerSessionId
                   coveredFrameCount = squash.CoveredFrameCount |}

    let main (value: obj) : obj =
        box
            {| kind = "Main"
               requestId =
                // No default: an absent requestId is handed through as null so
                // the inner contextOfJs derives canonical Main identity from
                // deltaDigest + coverage. Emitting a literal once pinned every
                // request to 'request-main', collapsing foreign claims into
                // Refreshed.
                if isNullish value?requestId then
                    null
                else
                    text value?requestId
               mainSession =
                if isNullish value?mainSession then
                    "ses-main"
                else
                    text value?mainSession
               bloggerSession =
                if isNullish value?bloggerSession then
                    "ses-blog"
                else
                    text value?bloggerSession
               toml = text value?toml
               previousIngested =
                int (
                    if isNullish value?previousIngested then
                        "0"
                    else
                        text value?previousIngested
                )
               nextIngested =
                int (
                    if isNullish value?nextIngested then
                        "1"
                    else
                        text value?nextIngested
                )
               previousCutoff =
                int (
                    if isNullish value?previousCutoff then
                        "0"
                    else
                        text value?previousCutoff
                )
               nextCutoff =
                int (
                    if isNullish value?nextCutoff then
                        "0"
                    else
                        text value?nextCutoff
                )
               nextDigest =
                if isNullish value?nextDigest then
                    ""
                else
                    text value?nextDigest
               frameEpoch =
                int (
                    if isNullish value?frameEpoch then
                        "0"
                    else
                        text value?frameEpoch
                )
               deltaDigest =
                if isNullish value?deltaDigest then
                    "delta"
                else
                    text value?deltaDigest
               observedEpoch =
                int (
                    if isNullish value?observedEpoch then
                        "0"
                    else
                        text value?observedEpoch
                ) |}

    let squash (value: obj) : obj =
        box
            {| kind = "Squash"
               requestId =
                // Same rule as `main`: absent requestId stays null so the
                // canonical squashRequestId derivation runs inside the ctx.
                if isNullish value?requestId then
                    null
                else
                    text value?requestId
               mainSession =
                if isNullish value?mainSession then
                    "ses-main"
                else
                    text value?mainSession
               bloggerSession =
                if isNullish value?bloggerSession then
                    "ses-blog"
                else
                    text value?bloggerSession
               frameEpoch =
                int (
                    if isNullish value?frameEpoch then
                        "0"
                    else
                        text value?frameEpoch
                )
               observedEpoch =
                int (
                    if isNullish value?observedEpoch then
                        "0"
                    else
                        text value?observedEpoch
                )
               coveredFrameCount =
                int (
                    if isNullish value?coveredFrameCount then
                        "0"
                    else
                        text value?coveredFrameCount
                )
               digests =
                if isNullish value?digests then
                    [||]
                else
                    unbox<obj array> value?digests |> Array.map text |}

    let toml (value: obj) : string = text value?toml

    /// Isolate physical shared-flight state before a semantic runtime test.
    let createScope () : obj =
        Wanxiangshu.OpenCode.SharedState.clearBloggerFlightsForTests ()
        box (new Wanxiangshu.Context.Companion.Blogger.OpenCode.PluginBloggerScope())

    let private parkWakeToJs (wake: ParkWake) : obj =
        match wake with
        | ParkWake.MaterialAvailable context ->
            box
                {| kind = "MaterialAvailable"
                   context = contextToJs context |}
        | ParkWake.Cancelled -> box {| kind = "Cancelled"; context = null |}

    let private offerDispositionName (disposition: MaterialOfferDisposition) =
        match disposition with
        | MaterialOfferDisposition.Delivered -> "Delivered"
        | MaterialOfferDisposition.Staged -> "Staged"

    let dispose (scope: obj) : unit =
        match hostOf scope with
        | :? IDisposable as d -> d.Dispose()
        | :? Wanxiangshu.Context.Companion.Blogger.OpenCode.PluginBloggerScope as pbs -> pbs.Dispose()
        | _ -> ()

    let park (scope: obj) (sessionId: string) : Task<obj> =
        task {
            let! wake = hostOf scope |> fun host -> host.ParkTransform sessionId
            return parkWakeToJs wake
        }

    let cancelParked (scope: obj) (sessionId: string) : unit =
        hostOf scope |> fun host -> host.CancelParked sessionId

    let offerMaterial (scope: obj) (sessionId: string) (context: obj) : string =
        hostOf scope
        |> fun host -> host.OfferMaterial(sessionId, contextOfJs context)
        |> offerDispositionName

    let private flightClaimName (claim: BloggerFlightClaim) =
        match claim with
        | BloggerFlightClaim.Claimed _ -> "Claimed"
        | BloggerFlightClaim.Refreshed _ -> "Refreshed"
        | BloggerFlightClaim.Conflict existing -> "Conflict:" + BloggerRequestId.value existing

    let claimCurrentRequest (scope: obj) (sessionId: string) (context: obj) : string =
        hostOf scope
        |> fun host -> host.ClaimCurrentRequest(sessionId, contextOfJs context)
        |> flightClaimName

    let claimFlight (scope: obj) (sessionId: string) (context: obj) : obj =
        match
            hostOf scope
            |> fun host -> host.ClaimCurrentRequest(sessionId, contextOfJs context)
        with
        | BloggerFlightClaim.Claimed lease
        | BloggerFlightClaim.Refreshed lease -> box lease
        | BloggerFlightClaim.Conflict _ -> null

    let acquireMaterialization (scope: obj) (sessionId: string) : Task<obj> =
        task {
            let! lease = hostOf scope |> fun host -> host.AcquireMaterialization sessionId
            return box lease
        }

    let releaseMaterialization (lease: obj) : unit =
        (unbox<BloggerMaterializationLease> lease).Release()

    let private flightReleaseName (release: BloggerFlightRelease) =
        match release with
        | BloggerFlightRelease.Released -> "Released"
        | BloggerFlightRelease.Missing -> "Missing"
        | BloggerFlightRelease.Conflict existing -> "Conflict:" + BloggerRequestId.value existing

    let releaseCurrentRequest (scope: obj) (sessionId: string) (requestId: string) : string =
        hostOf scope
        |> fun host -> host.ReleaseCurrentRequest(sessionId, BloggerRequestId.create requestId)
        |> flightReleaseName

    let beginBloggerShutdown (scope: obj) : unit =
        match hostOf scope with
        | :? Wanxiangshu.Context.Companion.Blogger.OpenCode.PluginBloggerScope as pbs -> pbs.BeginShutdown()
        | _ -> ()

    let claimRepairEpisode
        (scope: obj)
        (requestId: string)
        (authorityRoot: string)
        (mainSessionId: string)
        (bloggerSessionId: string)
        : string =
        let identity =
            { RequestId = BloggerRequestId.create requestId
              AuthorityRoot = AuthorityRootUserMessageId.create authorityRoot
              MainSessionId = SessionId.create mainSessionId
              BloggerSessionId = SessionId.create bloggerSessionId }

        match hostOf scope |> fun host -> host.ClaimRepairEpisode identity with
        | Ok _ -> "Claimed"
        | Error error -> "Error:" + error

    let drainRepairEpisodes (scope: obj) : Task =
        hostOf scope |> fun host -> host.DrainRepairEpisodes()

    let currentRequest (scope: obj) (sessionId: string) : obj =
        match hostOf scope |> fun host -> host.TryPeekCurrentRequest sessionId with
        | None -> null
        | Some value -> contextToJs value

    let scope () : obj = createScope ()

    let setPendingOffer (scope: obj) (sessionId: string) (context: obj) : string = offerMaterial scope sessionId context

    let offerParked (scope: obj) (sessionId: string) (context: obj) : string = offerMaterial scope sessionId context

    let tryGetFlight (scope: obj) (sessionId: string) : obj = currentRequest scope sessionId

    let peekCurrentRequest (scope: obj) (sessionId: string) : obj = currentRequest scope sessionId

    let createCompanion (sessionId: string) : obj =
        box (Companion(?sessionId = Some(SessionId.create sessionId)))
