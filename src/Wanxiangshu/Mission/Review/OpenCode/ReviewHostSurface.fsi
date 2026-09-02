namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Host-owned review guard boundary. Journal capabilities, transport ports, and
/// SharedState reservations remain opaque; semantic outcomes are plain records.
[<RequireQualifiedAccess>]
module ReviewHostSurface =

    val admittedWithReceipt: value: string -> Outcome.SendOutcome

    val admittedWithPhysicalMessage: value: string -> Outcome.SendOutcome

    val sessionId: value: string -> SessionId

    val reviewBarrierId: value: string -> ReviewBarrierId

    val worktreePath: value: string -> WorktreePath

    val setSessionDirectory: sessionId: string -> directory: string -> unit

    val clearSessionDirectory: sessionId: string -> unit

    val clearGuardNudges: unit -> unit

    val reverify:
        manager: obj -> jobId: string -> managerSession: string -> worktree: string -> barrier: string -> Task<obj>

    val openBarrier:
        handle: JournalHandle ->
        managerSession: string ->
        reviewerSession: string ->
        barrier: string ->
        tree: string ->
            Task<obj>

    val nudgeReviewer:
        port: obj -> handle: JournalHandle option -> reviewerSession: string -> terminalProviderRun: string -> Task<obj>

    val deliverJudgement:
        reviewerSession: string ->
        physicalUserMessage: string ->
        providerRun: string ->
        toolCall: string ->
        verdictText: string ->
            Task<obj> option
