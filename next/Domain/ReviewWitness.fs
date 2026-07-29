namespace Wanxiangshu.Next.Domain

/// Uniquely identifies a review barrier (one complete review cycle)
type ReviewBarrierId = ReviewBarrierId of string

module ReviewBarrierId =
    let create (value: string) = ReviewBarrierId value
    let value (ReviewBarrierId value) = value

/// A single witnessed verdict with proof of execution.
/// The authority root and physical user message are retained so the
/// projection can prove causal confirmation without persisted booleans.
type VerdictWitness = {
    ProviderRunId: string
    ToolCallId: string
    GitTreeHash: string
    AuthorityRootUserMessageId: string option
    UserMessageId: string option
}

/// Structured review witness — the ONLY authority for review state
type ReviewWitness =
    | NoReview
    | RevisionWitness of {| Report: string; GitTreeHash: string |}
    | PerfectPending of first: VerdictWitness
    | Confirmed of
        {| BarrierId: ReviewBarrierId
           First: VerdictWitness
           Second: VerdictWitness
           TreeHash: string |}

module ReviewWitness =
    let isConfirmed (w: ReviewWitness) : bool =
        match w with
        | Confirmed _ -> true
        | _ -> false

    let isPerfectPending (w: ReviewWitness) : bool =
        match w with
        | PerfectPending _ -> true
        | _ -> false

    let isRevision (w: ReviewWitness) : bool =
        match w with
        | RevisionWitness _ -> true
        | _ -> false

    let getGitTreeHash (w: ReviewWitness) : string option =
        match w with
        | Confirmed c -> Some c.TreeHash
        | PerfectPending p -> Some p.GitTreeHash
        | RevisionWitness r -> Some r.GitTreeHash
        | NoReview -> None

    let invalidateByTreeChange (w: ReviewWitness) (currentTree: string) : ReviewWitness =
        match w with
        | Confirmed c when c.TreeHash <> currentTree -> NoReview
        | PerfectPending p when p.GitTreeHash <> currentTree -> NoReview
        | RevisionWitness r when r.GitTreeHash <> currentTree -> NoReview
        | _ -> w

    let isDistinctWitness (a: VerdictWitness) (b: VerdictWitness) : bool =
        a.ProviderRunId <> b.ProviderRunId
        && a.ToolCallId <> b.ToolCallId

    /// True when `second` is a valid confirming witness for `first`.
    /// It must be a distinct provider run and tool call, on the same tree,
    /// and share the same authority root (or be a Host-accepted ReviewConfirmation
    /// continuation bound to the first's root).
    let canConfirm (firstRootAuthority: string option) (second: VerdictWitness) (first: VerdictWitness) : bool =
        if not (isDistinctWitness second first) then
            false
        elif second.GitTreeHash <> first.GitTreeHash then
            false
        else
            let sameRoot =
                match first.AuthorityRootUserMessageId, second.AuthorityRootUserMessageId with
                | Some r1, Some r2 when r1 = r2 -> true
                | _ -> false

            // If the first root is unknown, fail-closed: a confirmed second cannot
            // be proven to belong to the same logical run.
            if sameRoot then
                true
            else
                match first.AuthorityRootUserMessageId, firstRootAuthority with
                | Some r1, Some r2 when r1 = r2 ->
                    // The first's root matches the authority under which the second
                    // physical message was accepted as a ReviewConfirmation.
                    match second.UserMessageId with
                    | Some userMsg -> userMsg = r2
                    | None -> false
                | _ -> false
