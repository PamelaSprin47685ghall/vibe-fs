namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// REVIEW-003: the skeptical challenge a first PERFECT issues as its tool result.
///
/// Prose lives in `resources/provider/review/challenge/{en,zh-CN}.md` (PROMPT-019).
/// This module owns the semantic path, the text version, prompt assembly, and
/// the digest. Two distant call sites must agree exactly: the first PERFECT
/// journals `ChallengeContentDigest`, and the second PERFECT's input seal is
/// searched for that same value. Digest bytes are the ARCH-010 Prompt of the
/// session language, not a frozen English literal.
[<RequireQualifiedAccess>]
module ReviewChallenge =

    /// PROMPT-019 semantic path. Plain `let`, not `[<Literal>]`: Fable inlines a
    /// literal and emits no export, so a layer 1 test could not read the value.
    let Path = "review/challenge"

    /// REVIEW-003. Bump only with a migration: an older run's seal contains the
    /// older digest, and the version is what tells them apart. English canonical
    /// bytes unchanged → stay at 1; a new locale is not a new generation.
    let TextVersion = 1

    /// ARCH-010 instruction form of one already-localized sentence (`# text\n`).
    let promptOf (text: string) : string = SyntheticToml.document [ text ] []

    /// The digest recorded in `PerfectChallengeIssued` and searched for in the
    /// second run's seal.
    ///
    /// Delegates to `ProviderProjection.toolResultDigest` rather than hashing
    /// here. The recorded digest must be the hash of the exact final TOML bytes
    /// (`prompt`), because the second run's seal is built from those same bytes.
    /// A second hash or the bare text would silently refuse every confirmation
    /// while looking like correct fail-closed behaviour.
    let contentDigest (sha256: string -> string) (prompt: string) : SealDigest =
        ProviderProjection.toolResultDigest sha256 prompt
