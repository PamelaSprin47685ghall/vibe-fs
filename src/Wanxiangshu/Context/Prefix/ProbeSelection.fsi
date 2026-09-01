namespace Wanxiangshu.Context.Prefix

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type NoCandidateReason =
    | NoCoverage
    | CoverageNotAheadOfRequest
    | WouldRetreat of committed: int * proposed: int
    | NotNewerThanCommitted
    | CutoffProofFailed of expected: string * recomputed: string

[<RequireQualifiedAccess>]
module PrefixProbeSelection =
    val select:
        sha256: (string -> string) ->
        mainSessionId: SessionId ->
        committedEpoch: PrefixEpochId ->
        committedSnapshot: PrefixSnapshot option ->
        coverableCutoff: int ->
        coveredDigest: string ->
        requestStartCutoff: int ->
        frozenRecordPrefixRef: BlobRef ->
        frozenRecordPrefixDigest: BlobDigest ->
        recomputeDigest: (int -> string) ->
        Result<PrefixProbe, NoCandidateReason>

    val describeNoCandidate: reason: NoCandidateReason -> string
