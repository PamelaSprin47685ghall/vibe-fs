namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
    ///
    /// Plain `let`, not `[<Literal>]`: Fable inlines a literal and emits no export,
    /// so a layer 1 test could not read the value it must pin.
    let TextVersion = 1

    /// Domain 单源：`ProjectionConstants.ReviewChallengeText`（PROJ-008 Step5）。
    let Text = ProjectionConstants.ReviewChallengeText

    /// The final ARCH-010 form of the challenge as both a `verdict` tool result
    /// and a reviewer nudge prompt: an instruction-only TOML comment, exactly the
    /// bytes the second run's input seal will be searched for.
    /// Domain 单源：`ProjectionConstants.ReviewChallengePrompt`（与 algebra 渲染字节一致）。
    let Prompt = ProjectionConstants.ReviewChallengePrompt

    /// The digest recorded in `PerfectChallengeIssued` and searched for in the
    /// second run's seal.
    ///
    /// Delegates to `ProviderProjection.toolResultDigest` rather than hashing
    /// here. The recorded digest must be the hash of the exact final TOML bytes
    /// (`Prompt`), because the second run's seal is built from those same bytes.
    /// A second hash or the old raw text would silently refuse every confirmation
    /// while looking like correct fail-closed behaviour.
    let contentDigest (sha256: string -> string) : SealDigest =
        ProviderProjection.toolResultDigest sha256 Prompt
