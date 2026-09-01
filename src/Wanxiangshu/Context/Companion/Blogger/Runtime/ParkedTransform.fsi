namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ParkWake =
    | MaterialAvailable of BloggerRequestContext
    | Cancelled

[<RequireQualifiedAccess>]
type MaterialOfferDisposition =
    | Delivered
    | Staged

[<RequireQualifiedAccess>]
type BloggerFlightClaim =
    | Claimed
    | Refreshed
    | Conflict of BloggerRequestId

[<RequireQualifiedAccess>]
type BloggerFlightRelease =
    | Released
    | Missing
    | Conflict of BloggerRequestId

type BloggerMaterializationLease =
    internal new: release: (unit -> unit) -> BloggerMaterializationLease
    member Release: unit -> unit

type BloggerMaterializationAdmission =
    new: unit -> BloggerMaterializationAdmission
    member Acquire: sessionId: string -> Task<BloggerMaterializationLease>

type IBloggerRuntimeHost =
    abstract Cancellation: CancellationToken
    abstract ParkTransform: sessionId: string -> Task<ParkWake>
    abstract CancelParked: sessionId: string -> unit
    abstract HasParked: sessionId: string -> bool
    abstract HasFlight: sessionId: string -> bool
    abstract TryGetFlight: sessionId: string -> BloggerRequestContext option
    abstract ClaimCurrentRequest: sessionId: string * ctx: BloggerRequestContext -> BloggerFlightClaim
    abstract TryPeekCurrentRequest: sessionId: string -> BloggerRequestContext option
    abstract ReleaseCurrentRequest: sessionId: string * requestId: BloggerRequestId -> BloggerFlightRelease
    abstract AcquireMaterialization: sessionId: string -> Task<BloggerMaterializationLease>
    abstract OfferMaterial: sessionId: string * ctx: BloggerRequestContext -> MaterialOfferDisposition
    abstract TryTakePendingOffer: sessionId: string -> BloggerRequestContext option
    abstract GetDrainWindow: sessionId: string -> DrainWindow
    abstract SetDrainWindow: sessionId: string * window: DrainWindow -> unit
    abstract IsDrainOpen: sessionId: string -> bool

type ParkedTransform =
    new: sessionId: string -> ParkedTransform
    member SessionId: string
    member Completion: Task<ParkWake>
    member TryResume: context: BloggerRequestContext -> unit
    member TryCancel: unit -> unit
