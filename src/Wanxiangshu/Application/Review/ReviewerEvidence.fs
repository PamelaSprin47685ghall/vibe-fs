namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// Reviewer-side reads of the durable review guard.
///
/// Keyed by reviewer session, which is where REVIEW-003's facts land. The
/// previous version took the reviewer's parent from `sessionParents`, and on a
/// miss scanned every session's linkage to discover it — a full scan
/// (PERSIST-008) that also silently accepted a hit under the wrong parent. The
/// review conversation happens in the reviewer's session, so that is where its
/// state is read.
module ReviewerEvidence =

    let private guard (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> None
        | Some durable ->
            AgentProjection.tryFind (SessionId.create reviewerKey) (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)

    let private cohortHasRevision (projections: AgentProjectionSet) (request: FinalityRequestProjection) =
        request.Members
        |> Map.exists (fun reviewerSessionId memberRef ->
            AgentProjection.tryFind reviewerSessionId projections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.exists (fun reviewGuard ->
                reviewGuard.CurrentBarrierId = Some memberRef.BarrierId
                && ReviewWitness.isRevision reviewGuard.Witness))

    /// Whether the barrier still authorizes reviewer continuation. Finality may
    /// stop waiting after a sibling REVISE; that closed request must also revoke
    /// the reviewer's challenge capability. Non-Finality review owners have no
    /// ManagerLife projection and remain eligible.
    let continuationOpen journal reviewerKey =
        match journal, guard journal reviewerKey with
        | Some durable, Some reviewGuard ->
            match reviewGuard.CurrentManagerSessionId, reviewGuard.CurrentBarrierId with
            | Some managerSessionId, Some barrierId ->
                let snapshot = AgentJournal.snapshot durable

                let managerLife =
                    AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)

                match managerLife with
                | None -> true
                | Some lifecycle ->
                    // Finality may revoke continuation only for a reviewer that is
                    // actually a member of the current finality request at this
                    // barrier. Ordinary Orchestrator review barriers reuse the same
                    // completed Manager session, so "ManagerLife exists" is not
                    // evidence that this reviewer belongs to Finality.
                    match lifecycle.CurrentLife |> Option.bind (fun life -> life.ActiveFinality) with
                    | None -> true
                    | Some request ->
                        match Map.tryFind (SessionId.create reviewerKey) request.Members with
                        | None -> true
                        | Some enlisted when enlisted.BarrierId <> barrierId -> true
                        | Some _ ->
                            ManagerLifecycleProjection.isOpen request
                            && not (cohortHasRevision snapshot.AgentProjections request)
            | _ -> true
        | _ -> true

    /// Whether this reviewer has produced any verdict for the current barrier.
    ///
    /// Asked of `ObservedAttemptKeys` rather than of the witness: REVIEW-004
    /// records every counted attempt there, including a REVISE that later got
    /// superseded, so it answers "did the reviewer use the tool at all" without a
    /// second list to keep in step.
    let verdictSubmitted journal reviewerKey =
        guard journal reviewerKey
        |> Option.exists (fun reviewGuard -> not (List.isEmpty reviewGuard.ObservedAttemptKeys))

    /// REVIEW-003: a first PERFECT landed and its challenge is outstanding.
    let confirmationPending journal reviewerKey =
        guard journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            ReviewWitness.isPerfectPending reviewGuard.Witness
            && not reviewGuard.IsConfirmed)

    /// True once this reviewer's own dual-PERFECT is durably confirmed.
    ///
    /// Used to finish tool-only confirmation turns without waiting for a separate
    /// natural-language stop frame. The reviewer identity comes from the witness
    /// itself (REVIEW-006 self-containment), not from a stored
    /// `ConfirmedReviewerSessionId` beside it — a stored id can name a reviewer
    /// while the witness says there was no confirmation.
    let confirmed journal reviewerKey =
        guard journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            ReviewWitness.confirmedReviewer reviewGuard.Witness = Some(SessionId.create reviewerKey))

    /// Single evidence classification for ReviewerWorkflow (avoids behaviour-bool
    /// chain at the CE call site — DSL ownership). Order matches rabbit §9.2.
    [<RequireQualifiedAccess>]
    type Need =
        | CompleteConfirmed
        | EnsurePerfectConfirmed
        | EnsureVerdictSubmitted
        | CompleteRevision

    let classifyNeed journal reviewerKey =
        let reviewerId = SessionId.create reviewerKey

        let processReviewPending =
            match journal with
            | None -> None
            | Some durable ->
                MagicTodoProjection.pendingProcessReviewForReviewer
                    reviewerId
                    (AgentJournal.snapshot durable).AgentProjections.MagicTodo

        match processReviewPending, guard journal reviewerKey with
        | Some _, None -> Need.EnsureVerdictSubmitted
        | Some _, Some reviewGuard when List.isEmpty reviewGuard.ObservedAttemptKeys ->
            Need.EnsureVerdictSubmitted
        | Some _, Some _ ->
            // REVIEW-013: process PERFECT/REVISE is terminal. No confirmation nudge.
            Need.CompleteRevision
        | None, None -> Need.EnsureVerdictSubmitted
        | None, Some reviewGuard ->
            match ReviewWitness.confirmedReviewer reviewGuard.Witness with
            | Some id when id = reviewerId -> Need.CompleteConfirmed
            | _ ->
                match reviewGuard.Witness with
                | witness when ReviewWitness.isPerfectPending witness && not reviewGuard.IsConfirmed ->
                    Need.EnsurePerfectConfirmed
                | _ when List.isEmpty reviewGuard.ObservedAttemptKeys -> Need.EnsureVerdictSubmitted
                | _ -> Need.CompleteRevision
