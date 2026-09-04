namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Host-owned review guard boundary. Journal capabilities, transport ports, and
/// SharedState reservations remain opaque; semantic outcomes are plain records.
module HostReviewGuard =

    /// What a guard nudge send resolved to. `AlreadyOutstanding` is not a failure:
    /// the in-flight claim still drives the next turn. `Failed` means nothing is
    /// in flight, so a deferred completion must not be left waiting on a nudge
    /// that never landed.
    [<RequireQualifiedAccess>]
    type GuardNudgeOutcome =
        | Sent of PromptKey
        | AlreadyOutstanding
        | NoLongerRequired
        | Failed of reason: string

    val nudgeReviewer:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        sessionId: SessionId ->
        terminalProviderRun: ProviderRunIdentity ->
            Task<GuardNudgeOutcome>

    /// Infrastructure adapter only: expose Host delivery/dedupe as the typed
    /// ReviewerContinuationPort consumed by Application ReviewerWorkflow.
    val continuationPort:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
            ReviewerContinuationPort
