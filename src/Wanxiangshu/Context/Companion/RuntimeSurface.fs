namespace Wanxiangshu.Context.Companion

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// Context-compression runtime owner. One opaque PluginRuntimeScope owns the
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

    let private scopeOf (value: obj) : Wanxiangshu.OpenCode.PluginRuntimeScope =
        unbox<Wanxiangshu.OpenCode.PluginRuntimeScope> value

    let private hostOf (value: obj) : IBloggerRuntimeHost = (scopeOf value).BloggerRuntimeHost

    let private contextOfJs (value: obj) : BloggerRequestContext =
        match text value?kind with
        | "Squash" ->
            BloggerRequestContext.Squash
                { RequestId = BloggerRequestId.create (text value?requestId)
                  MainSessionId = SessionId.create (text value?mainSession)
                  BloggerSessionId = SessionId.create (text value?bloggerSession)
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  CoveredFrameCount = intValue value?coveredFrameCount
                  FrameDigests =
                    (if isNullish value?digests then
                         [||]
                     else
                         unbox<string array> value?digests)
                    |> Array.toList
                    |> List.map BlobDigest.create
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }
        | _ ->
            let items =
                match BloggerDeltaItemWire.tryListOfJs value?items with
                | Ok parsed -> parsed
                | Error error -> invalidArg "items" error

            BloggerRequestContext.Main
                { RequestId = BloggerRequestId.create (text value?requestId)
                  MainSessionId = SessionId.create (text value?mainSession)
                  BloggerSessionId = SessionId.create (text value?bloggerSession)
                  Items = items
                  Toml = text value?toml
                  PreviousIngestedThroughSequence = int64Value value?previousIngested
                  NextIngestedThroughSequence = int64Value value?nextIngested
                  PreviousCoverableTurnCutoffExclusive = intValue value?previousCutoff
                  NextCoverableTurnCutoffExclusive = intValue value?nextCutoff
                  NextCoveredPrefixDigest = text value?nextDigest
                  FrameEpochId = FrameEpochId.create (int64Value value?frameEpoch)
                  DeltaDigest = BlobDigest.create (text value?deltaDigest)
                  ObservedPrefixEpochId = PrefixEpochId.create (int64Value value?observedEpoch) }

    let private contextToJs (value: BloggerRequestContext) : obj =
        match value with
        | BloggerRequestContext.Main main ->
            box
                {| kind = "Main"
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
                   coveredFrameCount = squash.CoveredFrameCount |}

    let main (value: obj) : obj =
        box
            {| kind = "Main"
               requestId =
                if isNullish value?requestId then
                    "request-main"
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
                if isNullish value?requestId then
                    "request-squash"
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
        box (new Wanxiangshu.OpenCode.PluginRuntimeScope(None))

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
        (scopeOf scope :> IDisposable).Dispose()

    let park (scope: obj) (sessionId: string) : Task<obj> =
        task {
            let! wake = hostOf scope |> fun host -> host.ParkTransform sessionId
            return parkWakeToJs wake
        }

    let cancelParked (scope: obj) (sessionId: string) : unit =
        hostOf scope |> fun host -> host.CancelParked sessionId

    let hasParked (scope: obj) (sessionId: string) : bool =
        hostOf scope |> fun host -> host.HasParked sessionId

    let offerMaterial (scope: obj) (sessionId: string) (context: obj) : string =
        hostOf scope
        |> fun host -> host.OfferMaterial(sessionId, contextOfJs context)
        |> offerDispositionName

    let consumeStaged (scope: obj) (sessionId: string) : obj =
        match hostOf scope |> fun host -> host.TryTakePendingOffer sessionId with
        | None -> null
        | Some value -> contextToJs value

    let private flightClaimName (claim: BloggerFlightClaim) =
        match claim with
        | BloggerFlightClaim.Claimed -> "Claimed"
        | BloggerFlightClaim.Refreshed -> "Refreshed"
        | BloggerFlightClaim.Conflict existing -> "Conflict:" + BloggerRequestId.value existing

    let claimCurrentRequest (scope: obj) (sessionId: string) (context: obj) : string =
        hostOf scope
        |> fun host -> host.ClaimCurrentRequest(sessionId, contextOfJs context)
        |> flightClaimName

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

    let hasFlight (scope: obj) (sessionId: string) : bool =
        hostOf scope |> fun host -> host.HasFlight sessionId

    let currentRequest (scope: obj) (sessionId: string) : obj =
        match hostOf scope |> fun host -> host.TryPeekCurrentRequest sessionId with
        | None -> null
        | Some value -> contextToJs value

    let scope () : obj = createScope ()

    let setPendingOffer (scope: obj) (sessionId: string) (context: obj) : string = offerMaterial scope sessionId context

    let offerParked (scope: obj) (sessionId: string) (context: obj) : string = offerMaterial scope sessionId context

    let tryGetFlight (scope: obj) (sessionId: string) : obj = currentRequest scope sessionId

    let peekCurrentRequest (scope: obj) (sessionId: string) : obj = currentRequest scope sessionId

    let openDrain (root: string) : obj =
        box (BloggerRuntime.openDrain (AuthorityRootUserMessageId.create root))

    let closedDrain () : obj = box DrainWindow.Closed


    let setDrainWindow (scope: obj) (sessionId: string) (window: obj) : unit =
        hostOf scope
        |> fun host -> host.SetDrainWindow(sessionId, unbox<DrainWindow> window)

    let isDrainOpen (scope: obj) (sessionId: string) : bool =
        hostOf scope |> fun host -> host.IsDrainOpen sessionId

    let sealRuntime (scope: obj) (sessionId: string) : unit =
        BloggerRuntimeHost.forceSealRuntime (hostOf scope) sessionId

    let blocksNewRequest (durableSealed: bool) (hasFlightValue: bool) (drainOpenValue: bool) : bool =
        BloggerRuntime.blocksNewRequest durableSealed hasFlightValue drainOpenValue

    let decideMaterial
        (hasOpenProducerValue: bool)
        (hasParkedValue: bool)
        (hasFlightValue: bool)
        (context: obj)
        : string =
        match
            BloggerRuntime.decideMaterial hasOpenProducerValue hasParkedValue hasFlightValue (contextOfJs context)
        with
        | BloggerRuntime.Decision.Start _ -> "Start"
        | BloggerRuntime.Decision.Skip -> "Skip"
        | BloggerRuntime.Decision.Offer _ -> "Offer"

    let createCompanion (sessionId: string) : obj =
        box (Companion(?sessionId = Some(SessionId.create sessionId)))
