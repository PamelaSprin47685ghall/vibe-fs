namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Reviewer-side reads of the durable review guard.
///
/// Keyed by reviewer session, which is where REVIEW-003's facts land. The
/// previous version took the reviewer's parent from `sessionParents`, and on a
/// miss scanned every session's linkage to discover it — a full scan
/// (PERSIST-008) that also silently accepted a hit under the wrong parent. The
/// review conversation happens in the reviewer's session, so that is where its
/// state is read.
module ReviewerGuardState =

    let private guard (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> None
        | Some durable ->
            AgentProjection.tryFind (SessionId.create reviewerKey) (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)

    /// Whether this reviewer has produced any verdict for the current barrier.
    ///
    /// Asked of `ObservedAttemptKeys` rather than of the witness: REVIEW-004
    /// records every counted attempt there, including a REVISE that later got
    /// superseded, so it answers "did the reviewer use the tool at all" without a
    /// second list to keep in step.
    let submitted journal reviewerKey =
        guard journal reviewerKey
        |> Option.exists (fun reviewGuard -> not (List.isEmpty reviewGuard.ObservedAttemptKeys))

    /// REVIEW-003: a first PERFECT landed and its challenge is outstanding.
    let pendingConfirmation journal reviewerKey =
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
    let isConfirmedReviewer journal reviewerKey =
        guard journal reviewerKey
        |> Option.exists (fun reviewGuard ->
            ReviewWitness.confirmedReviewer reviewGuard.Witness = Some(SessionId.create reviewerKey))
