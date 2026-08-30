namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Persistence.Journal

/// JS-native entry for the Host fatal hook membrane.
/// Returned capabilities and adapter observations remain Host-owned data.
module PluginHooksSurface =
    /// Opaque Host-owned observation for the Blogger adapter proof.
    type BloggerAdapterObservation private (first: string, second: string) =
        member internal _.First = first
        member internal _.Second = second
        static member internal Create(first, second) = BloggerAdapterObservation(first, second)

    let fatalHook operation (adaptedHook: obj) : obj =
        PluginHostInterop.fatalHook operation adaptedHook

    let classifiedRejectionHook operation isExpected (adaptedHook: obj) : obj =
        PluginHostInterop.classifiedRejectionHook operation isExpected adaptedHook

    let private effectLabel =
        function
        | BloggerCoordinator.DecisionEffect.Started -> "Started"
        | BloggerCoordinator.DecisionEffect.StartedSquash -> "StartedSquash"
        | BloggerCoordinator.DecisionEffect.OfferedParked -> "OfferedParked"
        | BloggerCoordinator.DecisionEffect.NoMaterial -> "NoMaterial"
        | BloggerCoordinator.DecisionEffect.SkippedInFlight -> "SkippedInFlight"
        | BloggerCoordinator.DecisionEffect.Sealed -> "Sealed"
        | BloggerCoordinator.DecisionEffect.StartFailed reason -> "StartFailed:" + reason
        | BloggerCoordinator.DecisionEffect.MaterializeFailed reason -> "MaterializeFailed:" + reason

    /// Real Coordinator -> CompanionHost -> PromptDispatcher Host adapter. The
    /// same frozen context is offered twice while one physical flight remains
    /// unresolved, proving the second decision stops before Host submission.
    let coordinateBloggerUnresolvedTwice
        (port: obj)
        (handle: JournalHandle)
        (mainSession: string)
        (bloggerSession: string)
        (requestId: string)
        : Task<BloggerAdapterObservation> =
        task {
            let scope = new PluginRuntimeScope(None)
            let durable = AgentJournalCompanionPort handle.Journal :> ICompanionDurablePort
            let sessionPort = DispatchSurface.sessionPort port
            let satellites = SatelliteRuntime(sessionPort)

            let host =
                new CompanionHost(
                    SessionId.create mainSession,
                    sessionPort,
                    durable = durable,
                    restoredBloggerId = bloggerSession,
                    journal = handle.Journal,
                    satelliteRuntime = satellites
                )

            let context =
                BloggerRequestContext.Squash
                    { RequestId = BloggerRequestId.create requestId
                      MainSessionId = SessionId.create mainSession
                      BloggerSessionId = SessionId.create bloggerSession
                      FrameEpochId = FrameEpochId.create 1L
                      CoveredFrameCount = 1
                      FrameDigests = [ BlobDigest.create "blogger-effect-frame" ]
                      ObservedPrefixEpochId = PrefixEpochId.create 1L }

            let! first =
                BloggerCoordinator.onMainContext scope.BloggerRuntimeHost host (Some handle.Journal) context

            let! second =
                BloggerCoordinator.onMainContext scope.BloggerRuntimeHost host (Some handle.Journal) context

            return BloggerAdapterObservation.Create(effectLabel first, effectLabel second)
        }

    let firstBloggerEffect (observation: BloggerAdapterObservation) = observation.First

    let secondBloggerEffect (observation: BloggerAdapterObservation) = observation.Second
