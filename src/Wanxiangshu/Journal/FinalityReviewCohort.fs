namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// GLORY-043/044/045: the pure cohort algebra of one FinalityRequest.
///
/// The roster is real history (durable enlistments + confirmed witnesses); the
/// member decision is a local result of one Reviewer's protocol. Neither is a
/// program counter (GLORY-009).
module FinalityReviewCohort =

    /// GLORY-043: one Reviewer's terminal business result. Infrastructure
    /// failures are the only Error-like branch; REVISE is a legal result.
    [<RequireQualifiedAccess>]
    type ReviewerOutcome =
        | Revision of workRecord: string
        | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId

    /// One roster slot: a still-ungraduated historical Reviewer (session must
    /// be reused, GLORY-045) or the request's one new Reviewer.
    type CohortSlot =
        { AgentId: string
          ReviewerSessionId: SessionId option
          ReviewerOrdinal: int
          IsNew: bool }

    /// GLORY-045: a Reviewer graduated iff it has a confirmed witness on one of
    /// the barriers this Life enlisted it on. Derived from durable facts only.
    let graduatedReviewer
        (snapshot: AgentProjectionSet)
        (reviewerSessionId: SessionId)
        (standing: ReviewerStanding)
        : bool =
        match AgentProjection.tryFind reviewerSessionId snapshot with
        | None -> false
        | Some session ->
            match session.ReviewGuard with
            | None -> false
            | Some guard ->
                match guard.Witness with
                | ReviewWitness.Confirmed confirmed -> List.contains confirmed.BarrierId standing.Barriers
                | ReviewWitness.NoReview
                | ReviewWitness.RevisionWitness _
                | ReviewWitness.PerfectPending _ -> false

    /// GLORY-003/045: the roster of a new FinalityRequest =
    /// all still-ungraduated historical Reviewers of this Life
    /// + exactly one new Reviewer. The new Reviewer's ordinal is the next
    /// stable position (max enlisted ordinal + 1; 0 when the Life has none).
    let rosterOf
        (snapshot: AgentProjectionSet)
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        : CohortSlot list =
        let ungraduated =
            life.EnlistedReviewers
            |> Map.toList
            |> List.filter (fun (reviewerSessionId, standing) ->
                not (Map.containsKey reviewerSessionId request.Members)
                && not (graduatedReviewer snapshot reviewerSessionId standing))
            |> List.map (fun (reviewerSessionId, standing) ->
                { AgentId = standing.AgentId
                  ReviewerSessionId = Some reviewerSessionId
                  ReviewerOrdinal = standing.ReviewerOrdinal
                  IsNew = false })

        let alreadyCreatedNew =
            request.Members
            |> Map.toList
            |> List.tryPick (fun (_, m) -> if m.IsNewReviewer then Some m else None)

        let newSlot =
            match alreadyCreatedNew with
            | Some m ->
                // Crash re-entry: the request's new Reviewer already exists
                // (fork done, enlist durable). Reuse its session under the SAME
                // id it was forked with — never drift to a derived id, or the
                // durable HandleLinked becomes unreachable.
                { AgentId = sprintf "finality-new-%s" (FinalityRequestId.value request.RequestId)
                  ReviewerSessionId = Some m.ReviewerSessionId
                  ReviewerOrdinal = m.ReviewerOrdinal
                  IsNew = false }
            | None ->
                let nextOrdinal =
                    life.EnlistedReviewers
                    |> Map.toList
                    |> List.map (fun (_, standing) -> standing.ReviewerOrdinal)
                    |> function
                        | [] -> 0
                        | ordinals -> (List.max ordinals) + 1

                { AgentId = sprintf "finality-new-%s" (FinalityRequestId.value request.RequestId)
                  ReviewerSessionId = None
                  ReviewerOrdinal = nextOrdinal
                  IsNew = true }

        ungraduated @ [ newSlot ]
