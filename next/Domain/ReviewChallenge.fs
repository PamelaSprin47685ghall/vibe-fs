namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel.Identity

/// REVIEW-003: the skeptical challenge a first PERFECT issues as its tool result.
///
/// The sentence, its version, and its digest live together because they are one
/// fact viewed three ways, and two distant call sites must agree exactly: the
/// first PERFECT journals `ChallengeContentDigest`, and the second PERFECT's
/// input seal is searched for that same value.
[<RequireQualifiedAccess>]
module ReviewChallenge =

    /// REVIEW-003. Bump only with a migration: an older run's seal contains the
    /// older digest, and the version is what tells them apart.
    [<Literal>]
    let TextVersion = 1

    [<Literal>]
    let Text =
        "Nope, let's re-evaluate: does it really fully satisfy the original task without cutting corners?"

    /// The digest recorded in `PerfectChallengeIssued` and searched for in the
    /// second run's seal.
    ///
    /// Delegates to `ProviderProjection.toolResultDigest` rather than hashing
    /// here. The challenge IS a tool result, so sealing it necessarily produces
    /// this value; a second hash spelled locally would agree only by coincidence,
    /// and any drift would silently refuse every confirmation while looking like
    /// correct fail-closed behaviour.
    let contentDigest (sha256: string -> string) : SealDigest =
        ProviderProjection.toolResultDigest sha256 Text
