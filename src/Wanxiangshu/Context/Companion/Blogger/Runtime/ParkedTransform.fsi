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
type BloggerFlightRelease =
    | Released
    | Missing
    | Conflict of BloggerRequestId

type IBloggerFlightLease =
    inherit IDisposable
    abstract RequestId: BloggerRequestId

[<RequireQualifiedAccess>]
type BloggerFlightClaim =
    | Claimed of IBloggerFlightLease
    | Refreshed of IBloggerFlightLease
    | Conflict of BloggerRequestId

/// Exact live repair episode identity: one process-local episode per exact
/// request/authority (RequestId + AuthorityRoot + MainSessionId + BloggerSessionId).
type BloggerRepairEpisodeIdentity =
    { RequestId: BloggerRequestId
      AuthorityRoot: AuthorityRootUserMessageId
      MainSessionId: SessionId
      BloggerSessionId: SessionId }

/// Terminal/quiescence ports carried with an idle observation.
/// Root workspace crosses as a pure function port (TryRead shape): no
/// compile-order dependency on its contract and no unboxing.
type BloggerRepairTerminalPorts =
    { Quiescence: Wanxiangshu.OpenCode.ISessionQuiescenceGate
      Context: Wanxiangshu.Composition.Turn.ReconciledTurnContext
      SessionPort: Wanxiangshu.OpenCode.ISessionHostPort
      TryReadRootWorkspace: unit -> string option
      EventPort: Wanxiangshu.OpenCode.IEventObservationPort }

/// Repair observation: exact ProviderRun plus either transform facts or
/// terminal/quiescence ports. No business stage.
[<RequireQualifiedAccess>]
type BloggerRepairObservation =
    | TransformToolFacts of providerRun: ProviderRunIdentity * rawMessages: obj list
    | IdleQuiescentTurn of providerRun: ProviderRunIdentity * ports: BloggerRepairTerminalPorts

/// Per-envelope repair reply. No business stage.
[<RequireQualifiedAccess>]
type BloggerRepairOutcome =
    | NudgeSent of promptKey: PromptKey option
    | AabbSent of promptKey: PromptKey option
    | RepairInjected of obj list
    | PendingRepairWait
    | UnownedIdleIgnored
    | SupersededIgnored
    | AbandonedExhausted
    | Completed

/// One queued repair observation with its reply fence.
type BloggerRepairEnvelope =
    { Observation: BloggerRepairObservation
      Reply: TaskCompletionSource<BloggerRepairOutcome> }

/// FIFO TaskCompletionSource mailbox with one receiver CE and per-envelope
/// replies. Start is one-shot and claims the lifecycle; Post queues even
/// while the workflow is busy; Cancel settles every pending reply and receive;
/// Completion is always observable. The mailbox stores observations/replies only.
type BloggerRepairRendezvous =
    new: identity: BloggerRepairEpisodeIdentity * onCompleted: (unit -> unit) -> BloggerRepairRendezvous
    member Identity: BloggerRepairEpisodeIdentity
    member Completion: Task
    member Start: workflow: (unit -> Task) -> bool
    member Post: observation: BloggerRepairObservation -> Task<BloggerRepairOutcome>
    member Receive: unit -> Task<BloggerRepairEnvelope option>
    member Resolve: envelope: BloggerRepairEnvelope * outcome: BloggerRepairOutcome -> unit
    member Cancel: unit -> unit

type IBloggerRuntimeHost =
    abstract Cancellation: CancellationToken
    abstract ParkTransform: sessionId: string -> Task<ParkWake>
    abstract CancelParked: sessionId: string -> unit
    abstract TryGetFlight: sessionId: string -> BloggerRequestContext option
    abstract TryPeekCurrentRequest: sessionId: string -> BloggerRequestContext option
    abstract ClaimCurrentRequest: sessionId: string * ctx: BloggerRequestContext -> BloggerFlightClaim
    abstract ReleaseCurrentRequest: sessionId: string * requestId: BloggerRequestId -> BloggerFlightRelease
    abstract AcquireMaterialization: sessionId: string -> Task<BloggerMaterializationLease>
    abstract TryDeliverMaterial: sessionId: string * ctx: BloggerRequestContext -> bool
    abstract OfferMaterial: sessionId: string * ctx: BloggerRequestContext -> MaterialOfferDisposition
    abstract ClaimRepairEpisode: identity: BloggerRepairEpisodeIdentity -> Result<BloggerRepairRendezvous, string>
    abstract TryGetRepairEpisode: identity: BloggerRepairEpisodeIdentity -> BloggerRepairRendezvous option
    abstract CancelRepairEpisode: identity: BloggerRepairEpisodeIdentity -> unit
    abstract DrainRepairEpisodes: unit -> Task

type ParkedTransform =
    new: sessionId: string -> ParkedTransform
    member SessionId: string
    member Completion: Task<ParkWake>
    member TryResume: context: BloggerRequestContext -> unit
    member TryCancel: unit -> unit
