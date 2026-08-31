namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

open Wanxiangshu.Enforcer.EnforcerCodec

module EnforcerCycle =

    [<Literal>]
    let MaxBlogTextBytes = 512 * 1024

    [<Literal>]
    let MaxEvidenceBytes = 128 * 1024

    type ContentBounds = { TextBytes: int; EvidenceBytes: int }

    [<RequireQualifiedAccess>]
    type ContentBoundsRejection =
        | TextTooLarge
        | EvidenceTooLarge

    type CanonicalCycle =
        { MergedText: string
          CanonicalTip: EnforcerTip
          MergedEvidence: string }

    let ofCall (call: CanonicalBlogCall) : CanonicalCycle =
        { MergedText = call.Text |> Option.defaultValue ""
          CanonicalTip = call.Tip
          MergedEvidence = call.Evidence |> Option.defaultValue "" }

    let isValidCycle (cycle: CanonicalCycle) : bool = cycle.MergedText.Trim().Length > 0

    let contentBoundsError =
        function
        | ContentBoundsRejection.TextTooLarge -> sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes
        | ContentBoundsRejection.EvidenceTooLarge ->
            sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes

    // semantic-decorator-owner: behavior-diagnosis
    // semantic-decorator-WHAT: BD-011
    // semantic-decorator-trace-relation: count canonical text exactly once, then evidence exactly once, before applying the text-first rejection order
    // semantic-decorator-proof: requirements/behavior-diagnosis/tests/bounds.test.mjs::WHAT[BD-011] ENFORCER_042_bound_constants_match_utf8_byte_thresholds
    // semantic-decorator-failure-policy: a synchronous text count failure stops before evidence; an evidence count failure stops before either bounds decision
    // semantic-decorator-cancel-policy: pure synchronous byte counting introduces no cancellation boundary
    // semantic-decorator-deadline-policy: pure synchronous byte counting introduces no deadline
    // semantic-decorator-invocation-bound: 2
    let validateContentBounds
        (byteCount: string -> int)
        (text: string)
        (evidence: string)
        : Result<ContentBounds, ContentBoundsRejection> =
        let bounds =
            { TextBytes = byteCount text
              EvidenceBytes = byteCount evidence }

        if bounds.TextBytes > MaxBlogTextBytes then
            Error ContentBoundsRejection.TextTooLarge
        elif bounds.EvidenceBytes > MaxEvidenceBytes then
            Error ContentBoundsRejection.EvidenceTooLarge
        else
            Ok bounds
