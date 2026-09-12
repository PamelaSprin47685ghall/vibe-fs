namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Requirement.Grounding
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Context.Trace
open Wanxiangshu.Context.Prefix
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Context.Companion.Blogger

module AgentJournalPortAdapter =
    let forAttention (journal: AgentJournal) : AttentionJournalPort =
        { Read = fun () -> (AgentJournal.snapshot journal).AgentProjections.Attention
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.Attention fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> AttentionAppendFailure.DurabilityUnavailable)
                } }

    let forConcern (journal: AgentJournal) : ConcernJournalPort =
        { ReadState = fun sessionId -> (AgentJournal.snapshot journal).AgentProjections.Concern
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.Concern fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> ConcernAppendFailure.DurabilityUnavailable)
                } }

    let forInstitutionalLearning (journal: AgentJournal) : InstitutionalLearningJournalPort =
        { ReadState = fun sessionId -> (AgentJournal.snapshot journal).AgentProjections.InstitutionalLearning
          PendingAttentionWorkPairs =
            fun sessionId ->
                AgentProjection.pendingAttentionWorkPairs sessionId (AgentJournal.snapshot journal).AgentProjections
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.InstitutionalLearning fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> InstitutionalLearningAppendFailure.DurabilityUnavailable)
                } }

    let forDelegatedToolEstimate (journal: AgentJournal) : DelegatedToolEstimatePort =
        { TryState =
            fun sessionId ->
                AgentJournal.snapshot journal
                |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.DelegatedToolEstimate)
          Append =
            fun sessionId fact ->
                task {
                    let! result =
                        AgentJournal.appendAgent (StreamId.Session sessionId) None (AgentFact.Delegation fact) journal

                    return result |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                } }

    let forSessionStartedAt (journal: AgentJournal) : SessionStartedAtPort =
        { TryStartedAt =
            fun sessionId ->
                AgentJournal.snapshot journal
                |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.SessionStartedAt)
                |> Option.map SessionStartedAtProjection.startedAt
          Bind =
            fun sessionId candidate ->
                task {
                    let existing =
                        AgentJournal.snapshot journal
                        |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.SessionStartedAt)
                        |> Option.map SessionStartedAtProjection.startedAt

                    match existing with
                    | Some dt -> return Ok dt
                    | None ->
                        match!
                            AgentJournal.appendAgent
                                (StreamId.Session sessionId)
                                None
                                (HostFact.SessionStartedAtBound
                                    {| SessionId = sessionId
                                       StartedAt = candidate |})
                                journal
                        with
                        | Error error -> return Error(sprintf "%A" error)
                        | Ok projection ->
                            match
                                AgentProjection.tryFind sessionId projection.AgentProjections
                                |> Option.bind (fun session -> session.SessionStartedAt)
                                |> Option.map SessionStartedAtProjection.startedAt
                            with
                            | Some startedAt -> return Ok startedAt
                            | None -> return Error "SessionStartedAtBound did not materialize its projection"
                } }

    let forProviderFailure (journal: AgentJournal) : ProviderFailureJournalPort =
        { ProviderFailureJournalPort.CurrentState =
            fun sessionId ->
                AgentJournal.snapshot journal
                |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ProviderFailures)
          ProviderFailureJournalPort.Append =
            fun sessionId providerRun fact ->
                task {
                    let! result =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            (Some providerRun)
                            (AgentFact.ProviderFailure fact)
                            journal

                    return result |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                } }

    /// TURN-OBSERVE: Each member performs exactly one journal snapshot read at call time;
    /// multiple fields within one member share that snapshot.
    let forTurnObservation (journal: AgentJournal) : TurnObservationJournalPort =
        let projections () =
            (AgentJournal.snapshot journal).AgentProjections

        { TryBloggerReceiptKind =
            fun sessionId providerRun ->
                (projections ()).Sessions
                |> Map.tryFind sessionId
                |> Option.bind (fun session -> session.BloggerCycles)
                |> Option.bind (BloggerCycleProjection.tryReceipt providerRun)
                |> Option.map (fun receipt -> receipt.Kind)
          TryContinuationKind =
            fun sessionId physicalUserMessageId ->
                (projections ()).Sessions
                |> Map.tryFind sessionId
                |> Option.bind (fun session -> session.PromptAuthority)
                |> Option.bind (fun authority -> Map.tryFind physicalUserMessageId authority.AcceptedContinuationIds)
          IsFissionActive =
            fun sessionId ->
                FissionProjection.tryActiveForOwner sessionId (projections ()).Fission
                |> Option.isSome }

    let forTerminalPolicy (journal: AgentJournal) : TerminalPolicyPort =
        let projections () =
            (AgentJournal.snapshot journal).AgentProjections

        let canonicalRoleOf (authority: PromptAuthority.PromptAuthorityProjection) =
            match authority.ActiveLogicalRun, authority.LastAuthorityProfile with
            | Some run, _ -> Some run.CanonicalRole
            | None, Some profile -> Some profile.CanonicalRole
            | None, None -> None

        { IsPoisoned = fun () -> journal.IsPoisoned
          HasListableHandles =
            fun sessionId ->
                AgentProjection.tryFind sessionId (projections ())
                |> Option.bind (fun session -> session.Handles)
                |> Option.defaultValue HandleProjection.empty
                |> HandleProjection.listable
                |> List.isEmpty
                |> not
          HasActiveOrchestratorJobs = fun () -> AgentProjection.hasActiveOrchestratorJobs (projections ())
          IsLinkedChild = fun sessionId -> Map.containsKey sessionId (projections ()).HandleByChildSession
          TryCanonicalRole =
            fun sessionId ->
                Map.tryFind sessionId (projections ()).Sessions
                |> Option.bind (fun session -> session.PromptAuthority)
                |> Option.bind canonicalRoleOf }

    let forHostJoinGuard (journal: AgentJournal) : HostJoinGuardJournalPort =
        { HasOutstandingJoinClaim =
            fun targetSessionId terminalProviderRun ->
                let projections = (AgentJournal.snapshot journal).AgentProjections

                let payloadDigest =
                    PromptAuthority.gateNudgePayloadDigest "runtime/background-join" terminalProviderRun

                AgentProjection.tryFind targetSessionId projections
                |> Option.bind (fun session -> session.PromptAuthority)
                |> Option.map (fun authority ->
                    authority.PendingClaims
                    |> Map.exists (fun _ claim ->
                        claim.Origin = PromptAuthority.PromptOrigin.Continuation
                                           PromptAuthority.ContinuationKind.JoinGuard
                        && claim.PayloadDigest = payloadDigest))
                |> Option.defaultValue false }

    let forRequirementGrounding (journal: AgentJournal) : RequirementGroundingPort =
        { RequirementGroundingPort.ReadState =
            fun sessionId ->
                AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind _.RequirementGrounding
                |> Option.defaultValue RequirementGroundingProjection.empty
          RequirementGroundingPort.AppendRequested =
            fun sessionId snapshot ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingRequested
                                {| SessionId = sessionId
                                   Snapshot = snapshot |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                }
          RequirementGroundingPort.AppendMaterialObserved =
            fun sessionId observation ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingMaterialObserved
                                {| SessionId = sessionId
                                   Observation = observation |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                }
          RequirementGroundingPort.AppendAnchored =
            fun sessionId occurrence ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingAnchored
                                {| SessionId = sessionId
                                   Occurrence = occurrence |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                } }

    let forSessionResume (journal: AgentJournal) : SessionResumeJournalPort =
        { TryResumeProfile =
            fun sessionId ->
                let projections = (AgentJournal.snapshot journal).AgentProjections

                PromptAuthorityLedger.activeProfile sessionId projections
                |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile sessionId projections)
          CandidateRecords =
            fun parentId ->
                AgentJournal.handleProjection journal parentId
                |> HandleProjection.linkedChildren
                |> List.filter (fun record ->
                    match record.Ownership with
                    | HandleOwnership.DurableParentHandle -> true
                    | HandleOwnership.HostOwnedHidden -> false) }

    let forWire (journal: AgentJournal) : WireJournalPort =
        let failurePort = forProviderFailure journal

        { ReadView =
            fun sessionId ->
                let snapshot = AgentJournal.snapshot journal
                let projections = snapshot.AgentProjections
                let sessionProj = AgentProjection.tryFind sessionId projections

                let isComp =
                    SessionAssociationProjection.isCompanion sessionId projections.Associations

                let activeProf = PromptAuthorityLedger.activeProfile sessionId projections
                let failState = sessionProj |> Option.bind (fun session -> session.ProviderFailures)

                let wireState =
                    sessionProj
                    |> Option.map (fun s ->
                        { XTrace = s.XTrace
                          Blog = s.Blog
                          PrefixEpoch = s.PrefixEpoch })

                { State = wireState
                  IsCompanion = isComp
                  ActiveAuthorityProfile = activeProf
                  ProviderFailureState = failState }
          ReadBlob = fun blobRef -> journal.Writer.BlobWriter.Read blobRef
          WriteBlob =
            fun content ->
                task {
                    let! res = journal.WriteBlob content

                    return
                        res
                        |> Result.map (fun r ->
                            { BlobRef = r.BlobRef
                              BlobDigest = r.BlobDigest })
                }
          CurrentProjection = fun xTrace -> XTraceMaterialization.currentProjection journal xTrace
          RecordConfirmedSuccess =
            fun sessionId providerRun ->
                task {
                    match failurePort.CurrentState sessionId with
                    | None -> return Error "NoActiveRun: no provider failure state for session"
                    | Some current when current.Budget.ConsecutiveFailureCount = 0 -> return Ok()
                    | Some current ->
                        let fact =
                            AgentFact.ProviderFailure(
                                ProviderFailureFactCases.SuccessRecorded
                                    {| SessionId = sessionId
                                       LogicalRunId = current.LogicalRunId
                                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                                       ProviderRun = providerRun |}
                            )

                        let! appended =
                            AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal

                        return
                            appended
                            |> Result.map (fun _ -> ())
                            |> Result.mapError JournalAppendFailure.describe
                }
          CommitPrefixRebase =
            fun sessionId providerRun rebase ->
                task {
                    let fact =
                        ContextFact.PrefixRebaseCommitted
                            {| SessionId = sessionId
                               PreviousEpochId = rebase.PreviousEpochId
                               NextEpochId = rebase.NextEpochId
                               FrozenRecordPrefixRef = rebase.FrozenRecordPrefixRef
                               FrozenRecordPrefixDigest = rebase.FrozenRecordPrefixDigest
                               CutoffExclusive = rebase.CutoffExclusive
                               CoveredPrefixDigest = rebase.CoveredPrefixDigest
                               SealRoot = rebase.SealRoot
                               SyntheticMessageId = rebase.SyntheticMessageId
                               ProbeId = rebase.ProbeId
                               SolvingProviderRun = providerRun |}

                    let! appended =
                        AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact journal

                    return
                        appended
                        |> Result.map (fun _ -> ())
                        |> Result.mapError JournalAppendFailure.describe
                } }

    /// DELEG-029: durable composition is the only place that wraps delegation fact
    /// cases into the outer routing union and adapts the journal handle.
    let fromAgentJournal (journal: AgentJournal) : AgentJournalPort =
        { AppendExecutionFact =
            fun sessionId fact ->
                task {
                    match!
                        AgentJournal.appendAgent (StreamId.Session sessionId) None (AgentFact.Execution fact) journal
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }
          HandleProjection = fun sessionId -> AgentJournal.handleProjection journal sessionId
          ReadBlob = fun blobRef -> journal.Writer.BlobWriter.Read blobRef
          WriteBlob =
            fun content ->
                task {
                    match! journal.WriteBlob content with
                    | Ok receipt -> return Ok(receipt.BlobRef, receipt.BlobDigest)
                    | Error err -> return Error err
                }
          Sha256 = HostDigest.sha256Hex }
