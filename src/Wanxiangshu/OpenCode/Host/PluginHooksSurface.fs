namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.Journal

module PluginHooksSurface =

    /// Opaque Host-owned observation for the Blogger adapter proof.
    type BloggerAdapterObservation private (first: string, second: string) =
        member internal _.First = first
        member internal _.Second = second

        static member internal Create(first, second) =
            BloggerAdapterObservation(first, second)

    let policyAwareHook operation (adaptedHook: obj) : obj =
        PluginHostInterop.policyAwareHook operation adaptedHook

    let providerInputRejection message : obj =
        MagicTodoHostCodec.ProviderInputRejection message

    let hookFailurePolicy failure settlement : string =
        let typedFailure =
            match failure with
            | "LocalInvariant" -> ExecutionFailure.LocalInvariant
            | "ProtocolRejection" -> ExecutionFailure.ProtocolRejection
            | "UserCancelled" -> ExecutionFailure.UserCancelled
            | "Superseded" -> ExecutionFailure.Superseded
            | "CapacityQueueFull" -> ExecutionFailure.CapacityQueueFull
            | "AcceptanceUnknown" -> ExecutionFailure.AcceptanceUnknown
            | "PersistenceNotCommitted" -> ExecutionFailure.PersistenceFailure PersistenceCommitment.NotCommitted
            | "PersistenceCommitted" -> ExecutionFailure.PersistenceFailure PersistenceCommitment.Committed
            | "PersistenceUnknown" -> ExecutionFailure.PersistenceFailure PersistenceCommitment.Unknown
            | other -> invalidArg "failure" $"unknown hook proof failure '{other}'"

        let settlementEvidence =
            match settlement with
            | "NoOwnedExecution" -> PluginHostInterop.HookSettlementEvidence.NoOwnedExecution
            | "ExactSettlementComplete" -> PluginHostInterop.HookSettlementEvidence.ExactSettlementComplete
            | "DurableOutcomeUnknown" -> PluginHostInterop.HookSettlementEvidence.DurableOutcomeUnknown
            | "SettlementIncomplete" -> PluginHostInterop.HookSettlementEvidence.SettlementIncomplete
            | other -> invalidArg "settlement" $"unknown hook proof settlement '{other}'"

        let lifecycle =
            match settlementEvidence with
            | PluginHostInterop.HookSettlementEvidence.NoOwnedExecution -> DurableExecutionLifecycle.NoAcceptedFact
            | PluginHostInterop.HookSettlementEvidence.ExactSettlementComplete -> DurableExecutionLifecycle.Terminal
            | PluginHostInterop.HookSettlementEvidence.DurableOutcomeUnknown
            | PluginHostInterop.HookSettlementEvidence.SettlementIncomplete ->
                DurableExecutionLifecycle.AcceptedBeforeProvider

        let outcome: PluginHostInterop.HookFailureOutcome =
            { Failure = typedFailure
              Lifecycle = lifecycle
              ExecutionKey = None
              Settlement = settlementEvidence }

        match PluginHostInterop.interpretHookFailure outcome with
        | PluginHostInterop.HookFailurePolicy.RethrowUnchanged -> "RethrowUnchanged"
        | PluginHostInterop.HookFailurePolicy.FatalAfterSettlement -> "FatalAfterSettlement"
        | PluginHostInterop.HookFailurePolicy.RejectFatalBeforeSettlement -> "RejectFatalBeforeSettlement"

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

            let! first = CompanionTransform.coordinateBloggerContext scope host (Some handle.Journal) context

            let! second = CompanionTransform.coordinateBloggerContext scope host (Some handle.Journal) context

            return BloggerAdapterObservation.Create(effectLabel first, effectLabel second)
        }

    let firstBloggerEffect (observation: BloggerAdapterObservation) = observation.First

    let secondBloggerEffect (observation: BloggerAdapterObservation) = observation.Second
