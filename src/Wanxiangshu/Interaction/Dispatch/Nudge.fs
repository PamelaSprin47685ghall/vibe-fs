namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
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

/// ARCH-010: runtime instruction-only payload semantic paths (PROMPT-019).
///
/// Prose lives in `resources/provider/runtime/...`. Call sites bind language via
/// `ProviderProse.documentFor` / `instructionLines`. Domain owns path constants only.
///
/// REVIEW challenge prose is not a runtime nudge path: it lives in
/// `resources/provider/review/challenge` and is sent as a PromptAuthority continuation.
/// Language follows the Reviewer session.
///
/// ARCH-011: repair identity lives in typed `repairKind`, never in payload bytes.
[<RequireQualifiedAccess>]
module RuntimeNudge =

    /// FALLBACK: continuation after a provider failure inside one Logical Run.
    let ProviderRetry = "runtime/provider-retry"

    /// LOOP-006: continuation after a LOOP kill bridged into the same AABB path.
    let LoopContinue = "runtime/loop-continue"

    /// EXEC-016: join-capable role tried to finish while resources remain unjoined.
    let BackgroundJoin = "runtime/background-join"

    /// REVIEW-003: Reviewer produced prose where a structured verdict was required.
    let ReviewerVerdictRequired = "runtime/reviewer-verdict-required"

    /// Interaction repair after tool work with no final report.
    let MissingClosingReport = "runtime/missing-closing-report"

    /// Interaction repair that continues an in-progress turn.
    let InteractionContinue = "runtime/interaction-continue"
