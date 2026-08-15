namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.Persistence

#nowarn "3511"

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Review.Assurance
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// STRENGTH-002/006/009/010: Host boundary for one owner speculation opportunity.
/// All policy math stays in Domain; this adapter only freezes Host evidence,
/// invokes the decision-local Replica, publishes Prepared, and applies the
/// insertion intent after publication succeeds.
[<RequireQualifiedAccess>]
module StrengthSpeculate =

    let private zeroEstimate: StrengthValueEstimate = { V0 = 0.0; V1 = 0.0; V2 = 0.0 }

    let private decisionId
        (policyVersion: string)
        (owner: SessionId)
        (authorityRoot: AuthorityRootUserMessageId)
        (target: ProviderRunIdentity)
        =
        String.concat
            "\u001f"
            [ "strength-decision-v1"
              policyVersion
              SessionId.value owner
              AuthorityRootUserMessageId.value authorityRoot
              ProviderRunIdentity.value target ]
        |> HostDigest.sha256Hex
        |> StrengthDecisionId.create

    let private fastBinding
        (inventory: ManagedAgentConfig.ManagedAgentInventory)
        (role: Role)
        : (string * string * string) option =
        let fastName = ManagedAgent.nameOf AgentTier.Fast role
        let deepName = ManagedAgent.nameOf AgentTier.Deep role

        match Map.tryFind fastName inventory.Bindings, Map.tryFind deepName inventory.Bindings with
        | Some fast, Some deep -> Some(fastName, fast.Model, deep.Model)
        | _ -> None

    let private rootWork (sessionId: SessionId) (associations: Map<SessionId, SessionAssociation>) =
        match SessionOwnershipClassification.tryClassify sessionId associations with
        | Some(SessionExecutionClass.Work, Some SessionOwnership.Root) -> true
        | _ -> false

    let private failClosed (scope: PluginRuntimeScope) (reason: string) : 'a =
        scope.Strength.TripStrengthFuse reason
        raise (InvalidOperationException reason)

    let private renderCandidate
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (decision: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (output: obj)
        : Result<unit, string> =
        result {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let wire = ProviderWireCapture.decodeMessageView rawMessages

            let snapshot =
                { CurrentProjection = ProviderProjection.toSemantic wire
                  CommittedPrefix = None
                  BlogFrames = []
                  TransportMessages = Set.empty
                  HostReanchor = None }

            let rendered =
                ProjectionRenderer.renderMessagesWithHostIds
                    HostDigest.sha256Hex
                    snapshot
                    wire.Messages
                    [ ProjectionIntent.strengthCandidate owner decision target target bundle ]

            let! projected =
                ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase
                    (SessionId.value owner)
                    HostDigest.sha256Hex
                    rawMessages
                    rendered

            HostMessageProjection.replaceMessagesInPlace output projected
            return ()
        }

    [<RequireQualifiedAccess>]
    type private PreparedRecoveryIntent =
        | Absent
        | Settled
        | Consume of StrengthCandidateView

    let private preparedCandidateIntent
        (owner: SessionId)
        (decisionId: StrengthDecisionId)
        (projection: StrengthProjection)
        : Result<PreparedRecoveryIntent, string> =
        match StrengthProjection.tryCandidate decisionId projection with
        | None -> Error "Strength target index points to a missing Candidate"
        | Some view when view.Prepared.OwnerSessionId <> owner ->
            Error "Strength target index belongs to a different owner Session"
        | Some view when view.Promoted || view.Abandoned -> Ok PreparedRecoveryIntent.Settled
        | Some view -> Ok(PreparedRecoveryIntent.Consume view)

    let private preparedRecoveryIntent
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (projection: StrengthProjection)
        : Result<PreparedRecoveryIntent, string> =
        match StrengthProjection.tryDecisionForTarget target projection with
        | None -> Ok PreparedRecoveryIntent.Absent
        | Some id -> preparedCandidateIntent owner id projection

    let private wireAnchorDigest (rawMessages: obj list) =
        ProviderWireCapture.decodeMessageView rawMessages
        |> ProviderProjection.toSemantic
        |> ProviderProjection.renderSemantic
        |> HostDigest.sha256Hex

    let private ensureRecoveryAnchor (expected: string) (rawMessages: obj list) : Result<unit, string> =
        if wireAnchorDigest rawMessages <> expected then
            Error "Strength Prepared recovery anchor digest changed before target consumption"
        else
            Ok()

    let private recoverPrepared
        (durability: StrengthDurabilityPort)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (rawMessages: obj list)
        (projection: StrengthProjection)
        (output: obj)
        : Task<Result<bool, string>> =
        taskResult {
            let! intent = preparedRecoveryIntent owner target projection

            match intent with
            | PreparedRecoveryIntent.Absent -> return false
            | PreparedRecoveryIntent.Settled -> return true
            | PreparedRecoveryIntent.Consume view ->
                do! ensureRecoveryAnchor view.Prepared.AnchorDigest rawMessages
                let! bundle = durability.LoadFrameBundle view.Prepared
                do! renderCandidate owner target view.Prepared.DecisionId bundle output
                return true
        }

    type private BoundPorts =
        { Snapshots: ISessionSnapshotPort
          Durable: AgentJournal
          Runtime: StrengthReplicaRuntime
          Durability: StrengthDurabilityPort
          Inventory: ManagedAgentConfig.ManagedAgentInventory }

    type private OpportunitySurface =
        { Owner: SessionId
          Target: ProviderRunIdentity
          Authority: PromptAuthority.AuthorityExecutionProfile
          RawMessages: obj list
          Output: obj
          Scope: PluginRuntimeScope
          Ports: BoundPorts
          Projections: ProjectionSet
          Settings: StrengthRolloutConfig
          Opportunity: StrengthOpportunity
          Wire: ProviderProjection.ProviderWireProjection
          AnchorDigest: string
          Feature: StrengthFeatureKey
          Prediction: StrengthPrediction
          Estimate: StrengthValueEstimate
          Control: bool
          FastAgent: string option }

    [<RequireQualifiedAccess>]
    type private DryRunAdmission =
        | Skip of reason: string
        | Start of agent: string

    let private dryRunAdmission (eligibility: StrengthEligibility) (fastAgent: string option) : DryRunAdmission =
        match eligibility, fastAgent with
        | StrengthEligibility.Ineligible reason, _ -> DryRunAdmission.Skip reason
        | StrengthEligibility.Eligible, None -> DryRunAdmission.Skip "fast-peer-unavailable"
        | StrengthEligibility.Eligible, Some agent -> DryRunAdmission.Start agent

    [<RequireQualifiedAccess>]
    type private ReplicaMaterialDisposition =
        | TripInvalidFrame of reason: string
        | EmptyBatches
        | BundleInvalid of error: StrengthFrameError
        | BundleReady of StrengthFrameBundle

    let private bundleDisposition
        (maxFrameBytes: int)
        (batches: StrengthRequestBatch list)
        : ReplicaMaterialDisposition =
        match StrengthFrame.tryBuild HostDigest.sha256Hex maxFrameBytes batches with
        | Error error -> ReplicaMaterialDisposition.BundleInvalid error
        | Ok bundle -> ReplicaMaterialDisposition.BundleReady bundle

    let private replicaMaterialDisposition
        (maxFrameBytes: int)
        (completed: StrengthReplicaOutcome)
        : ReplicaMaterialDisposition =
        match completed.Terminal with
        | StrengthReplicaTerminal.InvalidFrame reason -> ReplicaMaterialDisposition.TripInvalidFrame reason
        | _ when List.isEmpty completed.Batches -> ReplicaMaterialDisposition.EmptyBatches
        | _ -> bundleDisposition maxFrameBytes completed.Batches

    let private applyPublishedCandidate
        (scope: PluginRuntimeScope)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (id: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (output: obj)
        =
        match renderCandidate owner target id bundle output with
        | Ok() -> ()
        | Error error -> failClosed scope ("Strength Candidate render failed closed: " + error)

    let private publishPreparedCandidate
        (durability: StrengthDurabilityPort)
        (scope: PluginRuntimeScope)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (id: StrengthDecisionId)
        (budget: StrengthBudget)
        (replicaSessionId: SessionId)
        (anchorDigest: string)
        (bundle: StrengthFrameBundle)
        (output: obj)
        : Task<unit> =
        task {
            let! published =
                durability.PublishPrepared
                    { OwnerSessionId = owner
                      DecisionId = id
                      TargetProviderRun = target
                      ReplicaSessionId = replicaSessionId
                      Budget = budget
                      AnchorDigest = anchorDigest
                      Bundle = bundle }

            match published with
            | StrengthPreparedPublish.StorageInvalid error ->
                failClosed scope ("Strength Prepared storage invalid: " + error)
            | StrengthPreparedPublish.Rejected _ -> return ()
            | StrengthPreparedPublish.Published ->
                applyPublishedCandidate scope owner target id bundle output
                return ()
        }

    let private observeDryRunCompletion (scope: PluginRuntimeScope) (owner: SessionId) (started: StrengthDryRunStart) =
        task {
            let! completed = started.Completion

            Diagnostic.emit
                "strength-dry-run-finished"
                [ "session_id", SessionId.value owner
                  "replica_session_id", SessionId.value started.ReplicaSessionId
                  "result", sprintf "%A" completed.Terminal ]

            match completed.Terminal with
            | StrengthReplicaTerminal.InvalidFrame reason ->
                scope.Strength.TripStrengthFuse("Strength dry-run invalid frame: " + reason)
            | _ -> ()
        }

    let private startDryRunWithMirror
        (surface: OpportunitySurface)
        (agent: string)
        (id: StrengthDecisionId)
        (replicaMirror: ProviderProjection.WireMessage list)
        : Task<unit> =
        task {
            let! startedResult =
                surface.Ports.Runtime.StartDryRun(
                    surface.Owner,
                    id,
                    surface.Target,
                    StrengthSettings.dryRunBudget (),
                    agent,
                    replicaMirror,
                    surface.AnchorDigest
                )

            match startedResult with
            | Error error ->
                Diagnostic.emit
                    "strength-dry-run-finished"
                    [ "session_id", SessionId.value surface.Owner
                      "result", "start-error:" + error ]

                return ()
            | Ok started ->
                // SPEC-INV-013: DryRun is a real, visible OpenCode child,
                // but terminal observation is not on the owner's transform
                // critical path. "Dry" means zero promotion while the shadow still executes for real.
                observeDryRunCompletion surface.Scope surface.Owner started |> ignore
                return ()
        }

    let private startDryRunReplica (surface: OpportunitySurface) (agent: string) : Task<unit> =
        task {
            let id =
                decisionId
                    surface.Settings.PolicyVersion
                    surface.Owner
                    surface.Authority.AuthorityRootUserMessageId
                    surface.Target

            let localized =
                StrengthFrame.tryLocalizeMirror HostDigest.sha256Hex id surface.AnchorDigest surface.Wire.Messages

            match localized with
            | Error _ -> return ()
            | Ok replicaMirror -> return! startDryRunWithMirror surface agent id replicaMirror
        }

    let private applyShadow (surface: OpportunitySurface) =
        let observationOpportunity =
            { surface.Opportunity with
                CostModelAvailable = true
                HostCanaryHealthy = true }

        match StrengthPolicy.eligibility observationOpportunity with
        | StrengthEligibility.Eligible ->
            surface.Scope.Strength.ArmStrengthCounterfactual(surface.Owner, surface.Target, surface.Feature)
        | StrengthEligibility.Ineligible _ -> ()

    let private applyDryRun (surface: OpportunitySurface) : Task<unit> =
        task {
            let canaryOpportunity =
                { surface.Opportunity with
                    CostModelAvailable = true
                    HostCanaryHealthy = true }

            match dryRunAdmission (StrengthPolicy.eligibility canaryOpportunity) surface.FastAgent with
            | DryRunAdmission.Skip reason ->
                Diagnostic.emit
                    "strength-dry-run-skip"
                    [ "session_id", SessionId.value surface.Owner; "result", reason ]

                return ()
            | DryRunAdmission.Start agent -> return! startDryRunReplica surface agent
        }

    let private consumeTreatmentMaterial
        (surface: OpportunitySurface)
        (id: StrengthDecisionId)
        (budget: StrengthBudget)
        (completed: StrengthReplicaOutcome)
        : Task<unit> =
        task {
            match replicaMaterialDisposition surface.Ports.Runtime.MaxFrameBytes completed with
            | ReplicaMaterialDisposition.TripInvalidFrame reason ->
                surface.Scope.Strength.TripStrengthFuse("Strength Replica invalid frame: " + reason)
                return ()
            | ReplicaMaterialDisposition.EmptyBatches -> return ()
            | ReplicaMaterialDisposition.BundleInvalid error ->
                surface.Scope.Strength.TripStrengthFuse(sprintf "Strength Replica bundle invalid: %A" error)
                return ()
            | ReplicaMaterialDisposition.BundleReady bundle ->
                return!
                    publishPreparedCandidate
                        surface.Ports.Durability
                        surface.Scope
                        surface.Owner
                        surface.Target
                        id
                        budget
                        completed.ReplicaSessionId
                        surface.AnchorDigest
                        bundle
                        surface.Output
        }

    let private startTreatmentWithMirror
        (surface: OpportunitySurface)
        (budget: StrengthBudget)
        (agent: string)
        (id: StrengthDecisionId)
        (replicaMirror: ProviderProjection.WireMessage list)
        : Task<unit> =
        task {
            let! outcome =
                surface.Ports.Runtime.StartDecision(
                    surface.Owner,
                    id,
                    surface.Target,
                    budget,
                    agent,
                    replicaMirror,
                    surface.AnchorDigest
                )

            match outcome with
            | Error _ -> return ()
            | Ok completed -> return! consumeTreatmentMaterial surface id budget completed
        }

    let private runTreatmentReplica
        (surface: OpportunitySurface)
        (budget: StrengthBudget)
        (agent: string)
        : Task<unit> =
        task {
            let id =
                decisionId
                    surface.Settings.PolicyVersion
                    surface.Owner
                    surface.Authority.AuthorityRootUserMessageId
                    surface.Target

            let localized =
                StrengthFrame.tryLocalizeMirror HostDigest.sha256Hex id surface.AnchorDigest surface.Wire.Messages

            match localized with
            | Error _ -> return ()
            | Ok replicaMirror -> return! startTreatmentWithMirror surface budget agent id replicaMirror
        }

    let private applyTreatment (surface: OpportunitySurface) : Task<unit> =
        task {
            let decision =
                StrengthPolicy.decideFromFacts
                    surface.Opportunity
                    surface.Control
                    false
                    surface.Prediction
                    surface.Estimate
                    surface.Settings.Policy

            match decision, surface.FastAgent with
            | StrengthDecision.Skip _, _ -> return ()
            | StrengthDecision.ControlHoldout, _ ->
                surface.Scope.Strength.ArmStrengthCounterfactual(surface.Owner, surface.Target, surface.Feature)
                return ()
            | StrengthDecision.Speculate(budget, _), None -> return ()
            | StrengthDecision.Speculate(budget, _), Some agent -> return! runTreatmentReplica surface budget agent
        }

    let private applyRollout (surface: OpportunitySurface) : Task<unit> =
        match surface.Settings.Mode with
        | StrengthRolloutMode.Shadow ->
            applyShadow surface
            task { return () }
        | StrengthRolloutMode.DryRun -> applyDryRun surface
        | StrengthRolloutMode.Off -> task { return () }
        | StrengthRolloutMode.Treatment -> applyTreatment surface

    let private planEvidence
        (scope: PluginRuntimeScope)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (projections: ProjectionSet)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        =
        match scope.TryAttemptPlan owner target with
        | Some plan ->
            plan.Profile.RequestKind, plan.Profile.EffectiveAgent, AttemptPlanner.probeOf plan |> Option.isSome
        | None -> ProviderRequestKind.WorkMain, FallbackEvidence.effectiveAgent owner projections authority, false

    let private buildOpportunity
        (scope: PluginRuntimeScope)
        (owner: SessionId)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (projections: ProjectionSet)
        (settings: StrengthRolloutConfig)
        (durable: AgentJournal)
        (rawMessages: obj list)
        (requestKind: ProviderRequestKind)
        (effectiveAgent: string)
        (hasPrefixProbe: bool)
        (fastAgent: string option)
        (modelsDistinct: bool)
        : StrengthOpportunity =
        let costsAvailable = settings.Costs.IsSome

        let stableCaptureEligible =
            XTraceCapture.supportsStableInsertion (Some durable) owner
            && (rawMessages |> List.forall (ProviderWireDecode.hostMessageId >> Option.isSome))

        { IsRootWork = rootWork owner projections.AgentProjections.Associations
          RequestKind = requestKind
          CanonicalRole = authority.CanonicalRole
          SelectedTier = authority.SelectedTier
          SelectedAgent = authority.SelectedAgent
          EffectiveAgent = effectiveAgent
          IsFallbackRetry = not (String.Equals(authority.SelectedAgent, effectiveAgent, StringComparison.Ordinal))
          HasPrefixProbe = hasPrefixProbe
          IsReviewerOrFinality = authority.CanonicalRole = Role.Reviewer
          IsAttachedOrInternalLeaf = not (rootWork owner projections.AgentProjections.Associations)
          OwnerCancelled = false
          TargetProviderRunBound = true
          EventStoreHealthy = true
          HostCanaryHealthy =
            StrengthSettings.hostCanaryHealthy ()
            && stableCaptureEligible
            && scope.Strength.StrengthFuseReason.IsNone
          FastPeerAvailable = fastAgent.IsSome
          ModelBindingsDistinct = modelsDistinct
          CostModelAvailable = costsAvailable }

    let private buildSurface
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (projections: ProjectionSet)
        (settings: StrengthRolloutConfig)
        (rawMessages: obj list)
        (output: obj)
        : OpportunitySurface =
        let requestKind, effectiveAgent, hasPrefixProbe =
            planEvidence scope owner target projections authority

        let fast = fastBinding ports.Inventory authority.CanonicalRole
        let fastAgent = fast |> Option.map (fun (name, _, _) -> name)

        let modelsDistinct =
            fast
            |> Option.exists (fun (_, fastModel, deepModel) ->
                not (String.Equals(fastModel, deepModel, StringComparison.Ordinal)))

        let opportunity =
            buildOpportunity
                scope
                owner
                authority
                projections
                settings
                ports.Durable
                rawMessages
                requestKind
                effectiveAgent
                hasPrefixProbe
                fastAgent
                modelsDistinct

        let wire = ProviderWireCapture.decodeMessageView rawMessages
        let semantic = ProviderProjection.toSemantic wire
        let semanticText = ProviderProjection.renderSemantic semantic
        let anchorDigest = HostDigest.sha256Hex semanticText

        let feature =
            scope.Strength.StrengthFeature(owner, authority.CanonicalRole, StrengthFrame.utf8ByteCount semanticText)

        let prediction = scope.Strength.StrengthPrediction feature

        let estimate =
            settings.Costs
            |> Option.map (StrengthRollout.estimate prediction)
            |> Option.defaultValue zeroEstimate

        let bucket =
            StrengthPolicy.controlBucket
                HostDigest.sha256Hex
                settings.PolicyVersion
                (AuthorityRootUserMessageId.value authority.AuthorityRootUserMessageId)
                (ProviderRunIdentity.value target)

        let control = StrengthPolicy.isControlHoldout settings.ControlRateBasisPoints bucket

        { Owner = owner
          Target = target
          Authority = authority
          RawMessages = rawMessages
          Output = output
          Scope = scope
          Ports = ports
          Projections = projections
          Settings = settings
          Opportunity = opportunity
          Wire = wire
          AnchorDigest = anchorDigest
          Feature = feature
          Prediction = prediction
          Estimate = estimate
          Control = control
          FastAgent = fastAgent }

    let private applyAfterRecovery
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (projections: ProjectionSet)
        (settings: StrengthRolloutConfig)
        (rawMessages: obj list)
        (output: obj)
        (recovered: bool)
        : Task<unit> =
        task {
            if recovered then
                return ()
            elif settings.Mode = StrengthRolloutMode.Off then
                return ()
            else
                let surface =
                    buildSurface scope ports owner target authority projections settings rawMessages output

                return! applyRollout surface
        }

    let private applyWithProjection
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (projections: ProjectionSet)
        (settings: StrengthRolloutConfig)
        (rawMessages: obj list)
        (output: obj)
        (durableStrength: StrengthProjection)
        : Task<unit> =
        task {
            // Recovery is independent of rollout state: once Prepared is
            // durable, only its bound target may consume the same bytes.
            match! recoverPrepared ports.Durability owner target rawMessages durableStrength output with
            | Error error -> failClosed scope ("Strength Prepared recovery failed closed: " + error)
            | Ok recovered ->
                return!
                    applyAfterRecovery
                        scope
                        ports
                        owner
                        target
                        authority
                        projections
                        settings
                        rawMessages
                        output
                        recovered
        }

    let private applyWithAuthority
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (authority: PromptAuthority.AuthorityExecutionProfile)
        (projections: ProjectionSet)
        (rawMessages: obj list)
        (output: obj)
        : Task<unit> =
        task {
            let settings = StrengthSettings.load ()

            match! ports.Durability.LoadProjection() with
            | Error error -> failClosed scope ("Strength opportunity cannot prove EventStore health: " + error)
            | Ok durableStrength ->
                return!
                    applyWithProjection
                        scope
                        ports
                        owner
                        target
                        authority
                        projections
                        settings
                        rawMessages
                        output
                        durableStrength
        }

    let private applyWithAssistant
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (rawMessages: obj list)
        (output: obj)
        (assistant: SessionMessage)
        : Task<unit> =
        task {
            let target = ProviderRunIdentity.create assistant.Id
            let projections = AgentJournal.snapshot ports.Durable

            match PromptAuthorityLedger.activeProfile owner projections.AgentProjections with
            | None -> return ()
            | Some authority ->
                return! applyWithAuthority scope ports owner target authority projections rawMessages output
        }

    let private applyWithSnapshotMessages
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (rawMessages: obj list)
        (output: obj)
        (physical: PhysicalUserMessageId)
        (messages: SessionMessage list)
        : Task<unit> =
        task {
            match ReviewSeal.bindableRun (PhysicalUserMessageId.value physical) messages with
            | Error _ -> return ()
            | Ok assistant -> return! applyWithAssistant scope ports owner rawMessages output assistant
        }

    let private applyWithPhysicalUser
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (rawMessages: obj list)
        (output: obj)
        (physical: PhysicalUserMessageId)
        : Task<unit> =
        task {
            let! snapshotResult = ports.Snapshots.GetMessages owner

            match snapshotResult with
            | Error _ -> return ()
            | Ok messages -> return! applyWithSnapshotMessages scope ports owner rawMessages output physical messages
        }

    let private applyPrimaryOwner
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (output: obj)
        : Task<unit> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output

            match ProviderWireCapture.lastUserMessageId rawMessages with
            | None -> return ()
            | Some physical -> return! applyWithPhysicalUser scope ports owner rawMessages output physical
        }

    let private applyBoundOwner
        (scope: PluginRuntimeScope)
        (ports: BoundPorts)
        (owner: SessionId)
        (output: obj)
        : Task<unit> =
        task {
            if ports.Runtime.IsReplica owner then
                return ()
            else
                return! applyPrimaryOwner scope ports owner output
        }

    let tryApply
        (snapshotPort: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            let candidates =
                journal,
                snapshotPort,
                scope.Strength.StrengthReplicaRuntime,
                strengthDurability,
                scope.Strength.ManagedAgentInventory,
                ProviderWireDecode.projectionSessionIdFromMessages output

            let bound =
                match candidates with
                | Some durable, Some snapshots, Some runtime, Some durability, Some inventory, Some sessionIdText ->
                    Some(
                        { Snapshots = snapshots
                          Durable = durable
                          Runtime = runtime
                          Durability = durability
                          Inventory = inventory },
                        SessionId.create sessionIdText
                    )
                | _ -> None

            match bound with
            | None -> return ()
            | Some(ports, owner) -> return! applyBoundOwner scope ports owner output
        }
