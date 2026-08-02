namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

/// Why no probe was built. Every case is a normal outcome, not an error.
///
/// CTX-011 is explicit that an armed slot with no candidate sends its ordinary main
/// request. So these are reasons for taking that path, and the caller treats all of
/// them alike — they are distinguished only so a diagnostic can say which one
/// happened (HOST-007).
[<RequireQualifiedAccess>]
type NoCandidateReason =
    /// Nothing complete has been consumed yet, or a reanchor voided what had been.
    | NoCoverage
    /// The Companion's cutoff sits at or past the current request's own start, so
    /// every covered turn is already inside the live tail.
    | CoverageNotAheadOfRequest
    /// CTX-011: the candidate would not move the covered range forward.
    | WouldRetreat of committed: int * proposed: int
    /// CTX-011: same cutoff, same prefix digest, same FrozenB digest as what is
    /// already committed. Promoting it would spend an epoch and a cold boundary for a
    /// prefix the model has already seen.
    | NotNewerThanCommitted
    /// COMPANION-011: the digest recomputed from X's current projection does not match
    /// the one the Companion recorded. Fail closed — the numbering moved under us.
    | CutoffProofFailed of expected: string * recomputed: string

/// CTX-011 candidate selection: build the probe for one armed slot, or say why not.
///
/// Pure. `sha256` is a parameter (VERIFY-008), and the FrozenB body is already
/// materialised by the caller — reading a blob is a Host concern (PERSIST-007), and
/// this module must stay callable from a layer-1 test.
[<RequireQualifiedAccess>]
module PrefixProbeSelection =

    /// CTX-011 snapshot identity: cutoff, covered-prefix digest, FrozenB digest.
    ///
    /// Same three fields `PrefixEpochProjection` compares. Spelled once here and
    /// consumed there would be better still, but the projection cannot depend on this
    /// module without a cycle, so the comment carries the obligation: these must stay
    /// in step.
    let private sameAsCommitted (candidate: PrefixSnapshot) (committed: PrefixSnapshot) =
        candidate.CutoffExclusive = committed.CutoffExclusive
        && candidate.CoveredPrefixDigest = committed.CoveredPrefixDigest
        && candidate.FrozenBDigest = committed.FrozenBDigest

    /// CTX-011, steps 1 through 9.
    ///
    /// `committedEpoch` / `committedSnapshot` are the two fields of the Journal's
    /// `ActivePrefixEpoch`, passed separately rather than as a Domain copy of that
    /// record. Domain cannot reference Journal, and a shadow type for one concept is
    /// exactly the duplication `PrefixSnapshot` was moved here to avoid: two records
    /// meaning "the committed prefix" could drift, and the fold would validate against
    /// one while the selector built from the other.
    ///
    /// `coverableCutoff` / `coveredDigest` come from the Companion's coverage.
    /// `requestStartCutoff` is how many turns precede this request's own physical user
    /// message — the candidate may not swallow the message being answered.
    /// `recomputeDigest` hashes X's CURRENT provider-visible prefix at a given cutoff;
    /// it is a function rather than a value because step 5 must hash exactly the
    /// candidate cutoff, which is only known after step 1.
    ///
    /// The proof in step 5 is the load-bearing check. Everything else compares numbers
    /// the plugin itself recorded; this one compares the Companion's claim against X's
    /// actual current prefix, and it is what makes a Host compaction or any other
    /// renumbering fail closed instead of producing a FrozenB that describes turns the
    /// prefix no longer has.
    let select
        (sha256: string -> string)
        (mainSessionId: SessionId)
        (committedEpoch: PrefixEpochId)
        (committedSnapshot: PrefixSnapshot option)
        (coverableCutoff: int)
        (coveredDigest: string)
        (requestStartCutoff: int)
        (frozenBRef: BlobRef)
        (frozenBDigest: BlobDigest)
        (recomputeDigest: int -> string)
        : Result<PrefixProbe, NoCandidateReason> =
        // Step 1. The candidate may cover no more than either side allows.
        let candidateCutoff = min coverableCutoff requestStartCutoff

        if coverableCutoff <= 0 then
            Error NoCandidateReason.NoCoverage
        elif candidateCutoff <= 0 then
            Error NoCandidateReason.CoverageNotAheadOfRequest
        else
            let committedCutoff =
                committedSnapshot
                |> Option.map (fun snapshot -> snapshot.CutoffExclusive)
                |> Option.defaultValue 0

            // Step 2a. CTX-011 forbids the covered range going backwards.
            if candidateCutoff < committedCutoff then
                Error(NoCandidateReason.WouldRetreat(committedCutoff, candidateCutoff))
            else
                // Step 5, before the identity comparison. The digest must be proven
                // against X's current prefix even when the candidate turns out to be
                // identical to what is committed: a matching identity computed from a
                // stale numbering is not evidence of anything.
                let recomputed = recomputeDigest candidateCutoff

                if recomputed <> coveredDigest then
                    Error(NoCandidateReason.CutoffProofFailed(coveredDigest, recomputed))
                else
                    // Steps 6 and 7. The seal is derived from the candidate's identity
                    // plus the epoch it was built from, so promoting it later needs no
                    // regeneration (CTX-012).
                    let sealRoot =
                        CompanionIdentity.sealRoot
                            sha256
                            mainSessionId
                            committedEpoch
                            candidateCutoff
                            coveredDigest
                            frozenBDigest

                    let candidate =
                        { FrozenBRef = frozenBRef
                          FrozenBDigest = frozenBDigest
                          CutoffExclusive = candidateCutoff
                          CoveredPrefixDigest = coveredDigest
                          SealRoot = sealRoot
                          SyntheticMessageId = CompanionIdentity.companionMemoryMessageId sha256 sealRoot }

                    // Step 2b. Equal cutoff with a tighter FrozenB is a legitimate new
                    // candidate — a Y squash makes B more compact without covering more
                    // X turns — so identity is the test, not the cutoff alone.
                    match committedSnapshot with
                    | Some existing when sameAsCommitted candidate existing ->
                        Error NoCandidateReason.NotNewerThanCommitted
                    | _ ->
                        // Step 8. The probe rides on the attempt profile (PROMPT-008),
                        // which is what keeps it valid for exactly one attempt.
                        Ok
                            { ProbeId = sha256 (sealRoot + "|probe")
                              BasedOnEpochId = committedEpoch
                              Candidate = candidate }

    let describeNoCandidate (reason: NoCandidateReason) =
        match reason with
        | NoCandidateReason.NoCoverage -> "no completed turn has been blogged yet"
        | NoCandidateReason.CoverageNotAheadOfRequest -> "every covered turn is already inside this request's tail"
        | NoCandidateReason.WouldRetreat(committed, proposed) ->
            sprintf "candidate cutoff %d is behind the committed %d (CTX-011)" proposed committed
        | NoCandidateReason.NotNewerThanCommitted -> "candidate is identical to the committed prefix (CTX-011)"
        | NoCandidateReason.CutoffProofFailed(expected, recomputed) ->
            sprintf
                "cutoff proof failed: Companion recorded %s but X's current prefix hashes to %s (COMPANION-011)"
                expected
                recomputed
