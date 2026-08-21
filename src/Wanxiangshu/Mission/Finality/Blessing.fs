namespace Wanxiangshu.Mission.Finality

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Resources
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

/// All-confirmed convergence: canonical records + stable tree → blessing.
module BlessingWorkflow =

    let private blessedPrompt (managerSessionId: SessionId) (logs: (int * string) list) =
        FinalityPrompt.blessedFromLogs
            (ProviderProse.instructionLines
                (ProviderProse.languageOf managerSessionId)
                FinalityPrompt.Path.Blessed
                Map.empty)
            logs

    let private checkAdmission
        (treePort: FinalityTreePort)
        (managerSessionId: SessionId)
        (witness: ConfirmedReviewWitness)
        : Result<BlessingPermit, string> =
        treePort.ReadManagerTree managerSessionId
        |> Result.mapError (fun error -> "Manager tree read failed: " + error)
        |> Result.bind (fun currentTree ->
            FinalityAdmission.grantBlessing currentTree witness
            |> Result.mapError (function
                | BlessingAdmissionFailure.StaleWitness(curr, expected) ->
                    sprintf
                        "Stale witness: current tree %s <> witness tree %s"
                        (GitTreeHash.value curr)
                        (GitTreeHash.value expected)
                | BlessingAdmissionFailure.IncompleteCohort reason -> "Incomplete cohort: " + reason))

    let private prepareBlessing
        (treePort: FinalityTreePort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (members: EnlistedMember list)
        (witness: ConfirmedReviewWitness)
        =
        taskResult {
            let! permit = checkAdmission treePort managerSessionId witness
            let! orderedRecords = RecordWorkflow.awaitCanonicalCohortRecords journal members

            let! orderedRecords =
                if List.length orderedRecords = List.length members then
                    Ok orderedRecords
                else
                    Error "Finality cohort record count changed"

            let! permit = checkAdmission treePort managerSessionId witness

            let logs =
                orderedRecords
                |> List.map (fun (ordinal, record) -> ordinal + 1, LlmFacing.normalizeNewlines record)

            let material = logs |> List.map snd |> String.concat "\n\n"
            let! blob = journal.WriteBlob material
            return logs, blob, permit
        }

    let blessIfAdmitted
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (witness: ConfirmedReviewWitness)
        (members: EnlistedMember list)
        : Task<FinalityOutcome> =
        task {
            match! prepareBlessing treePort journal managerSessionId members witness with
            | Error error -> return raise (InvalidOperationException("Finality blessing preparation failed: " + error))
            | Ok(logs, blob, permit) ->
                let lifeId = FinalityAdmission.permitLifeId permit
                let requestId = FinalityAdmission.permitRequestId permit
                let requestTree = FinalityAdmission.permitTree permit

                do!
                    FinalityJournal.appendLifecycle
                        journal
                        (ManagerLifecycleFact.FinalityBlessed
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               GitTreeHash = requestTree
                               WorkRecordBundleRef = blob.BlobRef
                               WorkRecordBundleDigest = blob.BlobDigest |})

                do! FinalityReviewerPort.abortAll reviewerPort members

                return FinalityOutcome.Blessed(blessedPrompt managerSessionId logs)
        }

    let blessIfTreeUnchanged
        (reviewerPort: FinalityReviewerPort)
        (treePort: FinalityTreePort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (members: EnlistedMember list)
        (requestTree: GitTreeHash)
        : Task<FinalityOutcome> =
        let memberWitnesses =
            members
            |> List.map (fun m ->
                let witness =
                    AgentProjection.tryFind m.ReviewerSessionId (AgentJournal.snapshot journal).AgentProjections
                    |> Option.bind (fun s -> s.ReviewGuard)
                    |> Option.map (fun g -> g.Witness)
                    |> Option.defaultValue ReviewWitness.NoReview

                (m.ReviewerSessionId, m.BarrierId, witness))

        match ConfirmedReviewWitness.create lifeId requestId requestTree memberWitnesses with
        | Error error ->
            raise (InvalidOperationException("Finality confirmed review witness projection failed: " + error))
        | Ok witness -> blessIfAdmitted reviewerPort treePort journal managerSessionId witness members
