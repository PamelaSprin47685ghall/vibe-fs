namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Review.Assurance
open Wanxiangshu.Persistence.Journal
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

    let private renderCandidate
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (decision: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (output: obj)
        : Result<unit, string> =
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

        match
            ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase
                (SessionId.value owner)
                HostDigest.sha256Hex
                rawMessages
                rendered
        with
        | Error error -> Error error
        | Ok projected ->
            HostMessageProjection.replaceMessagesInPlace output projected
            Ok()

    let private recoverPrepared
        (durability: StrengthDurabilityPort)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (rawMessages: obj list)
        (projection: StrengthProjection)
        (output: obj)
        : Task<Result<bool, string>> =
        task {
            match StrengthProjection.tryDecisionForTarget target projection with
            | None -> return Ok false
            | Some decisionId ->
                match StrengthProjection.tryCandidate decisionId projection with
                | None -> return Error "Strength target index points to a missing Candidate"
                | Some view when view.Prepared.OwnerSessionId <> owner ->
                    return Error "Strength target index belongs to a different owner Session"
                | Some view when view.Promoted || view.Abandoned -> return Ok true
                | Some view ->
                    let anchorDigest =
                        ProviderWireCapture.decodeMessageView rawMessages
                        |> ProviderProjection.toSemantic
                        |> ProviderProjection.renderSemantic
                        |> HostDigest.sha256Hex

                    if anchorDigest <> view.Prepared.AnchorDigest then
                        return Error "Strength Prepared recovery anchor digest changed before target consumption"
                    else
                        match! durability.LoadFrameBundle view.Prepared with
                        | Error error -> return Error error
                        | Ok bundle ->
                            return
                                renderCandidate owner target view.Prepared.DecisionId bundle output
                                |> Result.map (fun () -> true)
        }

    let tryApply
        (snapshotPort: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (output: obj)
        : Task<unit> =
        task {
            match
                journal,
                snapshotPort,
                scope.Strength.StrengthReplicaRuntime,
                strengthDurability,
                scope.Strength.ManagedAgentInventory,
                ProviderWireDecode.projectionSessionIdFromMessages output
            with
            | Some durable, Some snapshots, Some runtime, Some durability, Some inventory, Some sessionIdText ->
                let owner = SessionId.create sessionIdText

                if runtime.IsReplica owner then
                    return ()
                else
                    let rawMessages = ProviderWireDecode.messagesFromTransformOutput output

                    match ProviderWireCapture.lastUserMessageId rawMessages with
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

                                    match! durability.LoadProjection() with
                                    | Error error ->
                                        let reason = "Strength opportunity cannot prove EventStore health: " + error
                                        scope.Strength.TripStrengthFuse reason
                                        raise (InvalidOperationException reason)
                                    | Ok durableStrength ->
                                        // Recovery is independent of rollout state: once Prepared is
                                        // durable, only its bound target may consume the same bytes.
                                        match!
                                            recoverPrepared durability owner target rawMessages durableStrength output
                                        with
                                        | Error error ->
                                            let reason = "Strength Prepared recovery failed closed: " + error
                                            scope.Strength.TripStrengthFuse reason
                                            raise (InvalidOperationException reason)
                                        | Ok true -> return ()
                                        | Ok false when settings.Mode = StrengthRolloutMode.Off -> return ()
                                        | Ok false ->
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
                                                    not (String.Equals(fastModel, deepModel, StringComparison.Ordinal)))

                                            let costsAvailable = settings.Costs.IsSome

                                            let stableCaptureEligible =
                                                XTraceCapture.supportsStableInsertion (Some durable) owner
                                                && (rawMessages
                                                    |> List.forall (ProviderWireDecode.hostMessageId >> Option.isSome))

                                            let opportunity =
                                                { IsRootWork = rootWork owner projections.AgentProjections.Associations
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
                                                    not (rootWork owner projections.AgentProjections.Associations)
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

                                            let wire = ProviderWireCapture.decodeMessageView rawMessages
                                            let semantic = ProviderProjection.toSemantic wire
                                            let semanticText = ProviderProjection.renderSemantic semantic
                                            let anchorDigest = HostDigest.sha256Hex semanticText

                                            let feature =
                                                scope.Strength.StrengthFeature(
                                                    owner,
                                                    authority.CanonicalRole,
                                                    StrengthFrame.utf8ByteCount semanticText
                                                )

                                            let prediction = scope.Strength.StrengthPrediction feature

                                            let estimate =
                                                settings.Costs
                                                |> Option.map (StrengthRollout.estimate prediction)
                                                |> Option.defaultValue zeroEstimate

                                            let bucket =
                                                StrengthPolicy.controlBucket
                                                    HostDigest.sha256Hex
                                                    settings.PolicyVersion
                                                    (AuthorityRootUserMessageId.value
                                                        authority.AuthorityRootUserMessageId)
                                                    (ProviderRunIdentity.value target)

                                            let control =
                                                StrengthPolicy.isControlHoldout settings.ControlRateBasisPoints bucket

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
                                                    scope.Strength.ArmStrengthCounterfactual(owner, target, feature)
                                                | StrengthEligibility.Ineligible _ -> ()

                                                return ()
                                            | StrengthRolloutMode.DryRun ->
                                                // Host canary path: exercise the real nested Replica,
                                                // request profile, readonly tool loop and K1 stop, but
                                                // never publish Prepared or change primary bytes.
                                                let canaryOpportunity =
                                                    { opportunity with
                                                        CostModelAvailable = true
                                                        HostCanaryHealthy = true }

                                                match StrengthPolicy.eligibility canaryOpportunity, fastAgent with
                                                | StrengthEligibility.Ineligible reason, _ ->
                                                    Diagnostic.emit
                                                        "strength-dry-run-skip"
                                                        [ "session_id", SessionId.value owner; "result", reason ]

                                                    return ()
                                                | StrengthEligibility.Eligible, None ->
                                                    Diagnostic.emit
                                                        "strength-dry-run-skip"
                                                        [ "session_id", SessionId.value owner
                                                          "result", "fast-peer-unavailable" ]

                                                    return ()
                                                | StrengthEligibility.Eligible, Some agent ->
                                                    let id =
                                                        decisionId
                                                            settings.PolicyVersion
                                                            owner
                                                            authority.AuthorityRootUserMessageId
                                                            target

                                                    match
                                                        StrengthFrame.tryLocalizeMirror
                                                            HostDigest.sha256Hex
                                                            id
                                                            anchorDigest
                                                            wire.Messages
                                                    with
                                                    | Error _ -> return ()
                                                    | Ok replicaMirror ->
                                                        match!
                                                            runtime.StartDryRun(
                                                                owner,
                                                                id,
                                                                target,
                                                                StrengthSettings.dryRunBudget (),
                                                                agent,
                                                                replicaMirror,
                                                                anchorDigest
                                                            )
                                                        with
                                                        | Error error ->
                                                            Diagnostic.emit
                                                                "strength-dry-run-finished"
                                                                [ "session_id", SessionId.value owner
                                                                  "result", "start-error:" + error ]

                                                            return ()
                                                        | Ok started ->
                                                            // SPEC-INV-013: DryRun is a real, visible OpenCode child,
                                                            // but terminal observation is not on the owner's transform
                                                            // critical path. "Dry" means zero promotion while the shadow still executes for real.
                                                            task {
                                                                let! completed = started.Completion

                                                                Diagnostic.emit
                                                                    "strength-dry-run-finished"
                                                                    [ "session_id", SessionId.value owner
                                                                      "replica_session_id",
                                                                      SessionId.value started.ReplicaSessionId
                                                                      "result", sprintf "%A" completed.Terminal ]

                                                                match completed.Terminal with
                                                                | StrengthReplicaTerminal.InvalidFrame reason ->
                                                                    scope.Strength.TripStrengthFuse(
                                                                        "Strength dry-run invalid frame: " + reason
                                                                    )
                                                                | _ -> ()
                                                            }
                                                            |> ignore

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
                                                    scope.Strength.ArmStrengthCounterfactual(owner, target, feature)
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

                                                        match
                                                            StrengthFrame.tryLocalizeMirror
                                                                HostDigest.sha256Hex
                                                                id
                                                                anchorDigest
                                                                wire.Messages
                                                        with
                                                        | Error _ -> return ()
                                                        | Ok replicaMirror ->
                                                            let! outcome =
                                                                runtime.StartDecision(
                                                                    owner,
                                                                    id,
                                                                    target,
                                                                    budget,
                                                                    agent,
                                                                    replicaMirror,
                                                                    anchorDigest
                                                                )

                                                            match outcome with
                                                            | Error _ -> return ()
                                                            | Ok completed ->
                                                                match completed.Terminal with
                                                                | StrengthReplicaTerminal.InvalidFrame reason ->
                                                                    scope.Strength.TripStrengthFuse(
                                                                        "Strength Replica invalid frame: " + reason
                                                                    )

                                                                    return ()
                                                                | _ when List.isEmpty completed.Batches -> return ()
                                                                | _ ->
                                                                    match
                                                                        StrengthFrame.tryBuild
                                                                            HostDigest.sha256Hex
                                                                            runtime.MaxFrameBytes
                                                                            completed.Batches
                                                                    with
                                                                    | Error error ->
                                                                        scope.Strength.TripStrengthFuse(
                                                                            sprintf
                                                                                "Strength Replica bundle invalid: %A"
                                                                                error
                                                                        )

                                                                        return ()
                                                                    | Ok bundle ->
                                                                        match!
                                                                            durability.PublishPrepared
                                                                                { OwnerSessionId = owner
                                                                                  DecisionId = id
                                                                                  TargetProviderRun = target
                                                                                  ReplicaSessionId =
                                                                                    completed.ReplicaSessionId
                                                                                  Budget = budget
                                                                                  AnchorDigest = anchorDigest
                                                                                  Bundle = bundle }
                                                                        with
                                                                        | StrengthPreparedPublish.StorageInvalid error ->
                                                                            let reason =
                                                                                "Strength Prepared storage invalid: "
                                                                                + error

                                                                            scope.Strength.TripStrengthFuse reason
                                                                            raise (InvalidOperationException reason)
                                                                        | StrengthPreparedPublish.Rejected _ ->
                                                                            // Definite pre-intervention publication failure:
                                                                            // fail open to K0. No candidate bytes are visible.
                                                                            return ()
                                                                        | StrengthPreparedPublish.Published ->
                                                                            match
                                                                                renderCandidate
                                                                                    owner
                                                                                    target
                                                                                    id
                                                                                    bundle
                                                                                    output
                                                                            with
                                                                            | Ok() -> return ()
                                                                            | Error error ->
                                                                                // Prepared is durable but target has not been
                                                                                // allowed to leave this transform. Wrong/failed
                                                                                // rendering is therefore fail closed.
                                                                                let reason =
                                                                                    "Strength Candidate render failed closed: "
                                                                                    + error

                                                                                scope.Strength.TripStrengthFuse reason
                                                                                raise (InvalidOperationException reason)
            | _ -> return ()
        }
