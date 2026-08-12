namespace Wanxiangshu.Finality

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// All-confirmed convergence: canonical records + stable tree → blessing.
module BlessingWorkflow =

    let private undecidedPrompt (managerSessionId: SessionId) =
        ProviderProse.documentFor managerSessionId ManagerLifecyclePrompt.Path.FinalityUndecidable Map.empty

    let private blessedPrompt (managerSessionId: SessionId) (logs: (int * string) list) =
        FinalityPrompt.blessedFromLogs
            (ProviderProse.documentFor managerSessionId FinalityPrompt.Path.Blessed Map.empty)
            logs

    let private treeUnchanged (treePort: FinalityTreePort) (managerSessionId: SessionId) (expected: GitTreeHash) =
        match treePort.ReadManagerTree managerSessionId with
        | Ok current -> current = expected
        | Error _ -> false

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
        task {
            let undecided () =
                let reviewer, barrier =
                    match members with
                    | first :: _ -> first.ReviewerSessionId, first.BarrierId
                    | [] -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                RevisionWorkflow.concludeUndecided
                    journal
                    managerSessionId
                    lifeId
                    requestId
                    requestTree
                    reviewer
                    barrier

            if not (treeUnchanged treePort managerSessionId requestTree) then
                return! undecided ()
            else
                match! RecordWorkflow.awaitCanonicalCohortRecords journal members with
                | Error _ -> return! undecided ()
                | Ok orderedRecords when List.length orderedRecords <> List.length members -> return! undecided ()
                | Ok _ when not (treeUnchanged treePort managerSessionId requestTree) -> return! undecided ()
                | Ok orderedRecords ->
                    let logs =
                        orderedRecords
                        |> List.map (fun (ordinal, record) -> ordinal + 1, SyntheticToml.normalizeNewlines record)

                    let material = logs |> List.map snd |> String.concat "\n\n"

                    match journal.WriteBlob material with
                    | Error _ -> return FinalityOutcome.Undecided(undecidedPrompt managerSessionId)
                    | Ok blob ->
                        FinalityJournal.appendLifecycle
                            journal
                            (ManagerLifecycleFact.FinalityBlessed
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   GitTreeHash = requestTree
                                   WorkRecordBundleRef = blob.BlobRef
                                   WorkRecordBundleDigest = blob.BlobDigest |})

                        members
                        |> List.iter (fun memberInfo -> reviewerPort.AbortReviewer memberInfo.ReviewerSessionId)

                        return FinalityOutcome.Blessed(blessedPrompt managerSessionId logs)
        }
