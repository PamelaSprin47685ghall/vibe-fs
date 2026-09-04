namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

module FinalityJournal =

    val appendLifecycle: journal: AgentJournal -> fact: ManagerLifecycleFact -> Task

[<RequireQualifiedAccess>]
type RecordReadiness =
    | Ready of string
    | AwaitJournal
    | Unavailable of string

/// Canonical reviewer work-record materialization and journal-driven readiness.
module RecordWorkflow =

    val readiness:
        journal: AgentJournal ->
        snapshot: ProjectionSet ->
        reviewerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
        requiresTerminalFrontier: bool ->
            Task<RecordReadiness>

    val awaitCanonicalWorkRecord:
        journal: AgentJournal ->
        reviewerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
            Task<Result<string, string>>

    val awaitCanonicalCohortRecords:
        observer: IWaitObserver ->
        journal: AgentJournal ->
        members: EnlistedMember list ->
            Task<Result<(int * string) list, string>>
