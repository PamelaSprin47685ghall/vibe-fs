namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Requirement.Grounding
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Host

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
