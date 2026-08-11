namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Recovery
open Wanxiangshu.Session

/// STRENGTH-002/006/009/010: Host boundary for one owner speculation opportunity.
/// All policy math stays in Domain; this adapter only freezes Host evidence,
/// invokes the decision-local Replica, publishes Prepared, and applies the
/// insertion intent after publication succeeds.
[<RequireQualifiedAccess>]
module StrengthSpeculate =

    let private zeroEstimate : StrengthValueEstimate =
        { V0 = 0.0
          V1 = 0.0
          V2 = 0.0 }

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

    let private rootWork
        (sessionId: SessionId)
        (associations: Map<SessionId, SessionAssociation>)
        =
        match SessionOwnershipClassification.tryClassify sessionId associations with
        | Some(SessionExecutionClass.Work, Some SessionOwnership.Root) -> true
        | _ -> false

    let private renderCandidate
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (decision: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (output: obj)
        : Result<unit, string> =
        let rawMessages = Projection.messagesFromTransformOutput output
        let wire = Projection.decodeMessageView rawMessages
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

        match
            Projection.tryApplyRenderedInsertionsPreservingBase
                (SessionId.value owner)
                HostDigest.sha256Hex
                rawMessages
                rendered
        with
        | Error error -> Error error
        | Ok projected ->
            HostMessageProjection.replaceMessagesInPlace output projected
            Ok()

    let tryApply
        (snapshotPort: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            match
                journal,
                snapshotPort,
                scope.StrengthReplicaRuntime,
                scope.StrengthPersistence,
                scope.ManagedAgentInventory,
                Projection.projectionSessionIdFromMessages output
            with
            | Some durable, Some snapshots, Some runtime, Some(raw, store), Some inventory, Some sessionIdText ->
                let owner = SessionId.create sessionIdText

                if runtime.IsReplica owner then
                    return ()
                else
                    let rawMessages = Projection.messagesFromTransformOutput output

                    match Projection.lastUserMessageId rawMessages with
                    | None -> return ()
                    | Some physical ->
                        let! snapshotResult = snapshots.GetMessages owner

                        match snapshotResult with
                        | Error _ -> return ()
                        | Ok messages ->
                            match ReviewSeal.bindableRun (PhysicalUserMessageId.value physical) messages with
                            | Error _ -> return ()
                            | Ok assistant ->
                                let target = ProviderRunIdentity.create assistant.Id
                                let projections = AgentJournal.snapshot durable

                                match PromptAuthorityLedger.activeProfile owner projections.AgentProjections with
                                | None -> return ()
                                | Some authority ->
                                    let settings = StrengthSettings.load ()

                                    if settings.Mode = StrengthRolloutMode.Off then
                                        return ()
                                    else
                                        match StrengthStore.loadProjection raw (store.OpenSnapshot()) with
                                        | Error error ->
                                            raise (
                                                InvalidOperationException(
                                                    "Strength opportunity cannot prove EventStore health: " + error
                                                )
                                            )
                                        | Ok durableStrength ->
                                            // One TargetProviderRun may own at most one decision. A
                                            // durable Prepared from an earlier transform is replayed
                                            // through the Candidate path rather than spawning a new leaf.
                                            match StrengthProjection.tryDecisionForTarget target durableStrength with
                                            | Some _ -> return ()
                                            | None ->
                                                let currentPlan = scope.TryAttemptPlan owner target
                                                let requestKind, effectiveAgent, hasPrefixProbe =
                                                    match currentPlan with
                                                    | Some plan ->
                                                        plan.Profile.RequestKind,
                                                        plan.Profile.EffectiveAgent,
                                                        AttemptPlanner.probeOf plan |> Option.isSome
                                                    | None ->
                                                        ProviderRequestKind.WorkMain,
                                                        FallbackEvidence.effectiveAgent owner projections authority,
                                                        false

                                                let fast = fastBinding inventory authority.CanonicalRole
                                                let fastAgent = fast |> Option.map (fun (name, _, _) -> name)
                                                let modelsDistinct =
                                                    fast
                                                    |> Option.exists (fun (_, fastModel, deepModel) ->
                                                        not (
                                                            String.Equals(
                                                                fastModel,
                                                                deepModel,
                                                                StringComparison.Ordinal
                                                            )
                                                        ))

                                                let costsAvailable = settings.Costs.IsSome

                                                let opportunity =
                                                    { IsRootWork =
                                                        rootWork owner projections.AgentProjections.Associations
                                                      RequestKind = requestKind
                                                      CanonicalRole = authority.CanonicalRole
                                                      SelectedTier = authority.SelectedTier
                                                      SelectedAgent = authority.SelectedAgent
                                                      EffectiveAgent = effectiveAgent
                                                      IsFallbackRetry =
                                                        not (
                                                            String.Equals(
                                                                authority.SelectedAgent,
                                                                effectiveAgent,
                                                                StringComparison.Ordinal
                                                            )
                                                        )
                                                      HasPrefixProbe = hasPrefixProbe
                                                      IsReviewerOrFinality = authority.CanonicalRole = Role.Reviewer
                                                      IsAttachedOrInternalLeaf =
                                                        not (
                                                            rootWork
                                                                owner
                                                                projections.AgentProjections.Associations
                                                        )
                                                      OwnerCancelled = false
                                                      TargetProviderRunBound = true
                                                      EventStoreHealthy = true
                                                      HostCanaryHealthy = StrengthSettings.hostCanaryHealthy ()
                                                      FastPeerAvailable = fastAgent.IsSome
                                                      ModelBindingsDistinct = modelsDistinct
                                                      CostModelAvailable = costsAvailable }

                                                let wire = Projection.decodeMessageView rawMessages
                                                let semantic = ProviderProjection.toSemantic wire
                                                let semanticText = ProviderProjection.renderSemantic semantic
                                                let anchorDigest = HostDigest.sha256Hex semanticText
                                                let feature =
                                                    scope.StrengthFeature(
                                                        owner,
                                                        authority.CanonicalRole,
                                                        StrengthFrame.utf8ByteCount semanticText
                                                    )
                                                let prediction = scope.StrengthPrediction feature
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
                                                let control =
                                                    StrengthPolicy.isControlHoldout
                                                        settings.ControlRateBasisPoints
                                                        bucket

                                                match settings.Mode with
                                                | StrengthRolloutMode.Shadow ->
                                                    // Shadow never starts a Replica. It records only
                                                    // clean primary counterfactual labels for a fully
                                                    // eligible opportunity except economic activation.
                                                    let observationOpportunity =
                                                        { opportunity with
                                                            CostModelAvailable = true
                                                            HostCanaryHealthy = true }

                                                    match StrengthPolicy.eligibility observationOpportunity with
                                                    | StrengthEligibility.Eligible ->
                                                        scope.ArmStrengthCounterfactual(owner, target, feature)
                                                    | StrengthEligibility.Ineligible _ -> ()

                                                    return ()
                                                | StrengthRolloutMode.Off -> return ()
                                                | StrengthRolloutMode.Treatment ->
                                                    let decision =
                                                        StrengthPolicy.decideFromFacts
                                                            opportunity
                                                            control
                                                            false
                                                            prediction
                                                            estimate
                                                            settings.Policy

                                                    match decision with
                                                    | StrengthDecision.Skip _ -> return ()
                                                    | StrengthDecision.ControlHoldout ->
                                                        scope.ArmStrengthCounterfactual(owner, target, feature)
                                                        return ()
                                                    | StrengthDecision.Speculate(budget, _) ->
                                                        match fastAgent with
                                                        | None -> return ()
                                                        | Some agent ->
                                                            let id =
                                                                decisionId
                                                                    settings.PolicyVersion
                                                                    owner
                                                                    authority.AuthorityRootUserMessageId
                                                                    target

                                                            let! outcome =
                                                                runtime.StartDecision(
                                                                    owner,
                                                                    id,
                                                                    target,
                                                                    budget,
                                                                    agent,
                                                                    wire.Messages,
                                                                    anchorDigest
                                                                )

                                                            match outcome with
                                                            | Error _ -> return ()
                                                            | Ok completed when List.isEmpty completed.Batches ->
                                                                return ()
                                                            | Ok completed ->
                                                                match
                                                                    StrengthFrame.tryBuild
                                                                        HostDigest.sha256Hex
                                                                        runtime.MaxFrameBytes
                                                                        completed.Batches
                                                                with
                                                                | Error _ -> return ()
                                                                | Ok bundle ->
                                                                    let payload = StrengthStore.encodeFrameBundlePayload bundle

                                                                    match
                                                                        StrengthStore.publishWithPayloads
                                                                            store
                                                                            HostDigest.sha256Hex
                                                                            [ payload ]
                                                                            (fun refs ->
                                                                                StrengthEvents.prepared
                                                                                    owner
                                                                                    id
                                                                                    target
                                                                                    completed.ReplicaSessionId
                                                                                    budget
                                                                                    anchorDigest
                                                                                    bundle.Digest
                                                                                    bundle.ByteLength
                                                                                    refs)
                                                                    with
                                                                    | Error(PublishError.StorageInvalid error) ->
                                                                        raise (
                                                                            InvalidOperationException(
                                                                                sprintf
                                                                                    "Strength Prepared storage invalid: %A"
                                                                                    error
                                                                            )
                                                                        )
                                                                    | Error _ ->
                                                                        // Definite pre-intervention publication failure:
                                                                        // fail open to K0. No candidate bytes are visible.
                                                                        return ()
                                                                    | Ok _ ->
                                                                        match renderCandidate owner target id bundle output with
                                                                        | Ok() -> return ()
                                                                        | Error error ->
                                                                            // Prepared is durable but target has not been
                                                                            // allowed to leave this transform. Wrong/failed
                                                                            // rendering is therefore fail closed.
                                                                            raise (
                                                                                InvalidOperationException(
                                                                                    "Strength Candidate render failed closed: "
                                                                                    + error
                                                                                )
                                                                            )
            | _ -> return ()
        }
