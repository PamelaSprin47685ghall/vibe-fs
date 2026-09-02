namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module JoinDrain =
    val stableJoinKey: record: HandleRecord -> int * string
    val orderedCandidates: projection: AgentLinkageProjection -> HandleRecord list

    val tryConsumeOneAbandoned:
        durable: AgentJournal ->
        parentId: SessionId ->
        record: HandleRecord ->
        completedAt: DateTimeOffset ->
            Task<Result<RunCompletion, ForkError> option>

    val tryConsumeOneDurable:
        durable: AgentJournal ->
        parentId: SessionId ->
        record: HandleRecord ->
        completedAt: DateTimeOffset ->
            Task<Result<RunCompletion, ForkError> option>

    val tryConsumeOne:
        durable: AgentJournal ->
        parentId: SessionId ->
        completedAt: DateTimeOffset ->
        record: HandleRecord ->
            Task<Result<RunCompletion, ForkError> option>

    val drainJoinableBatch:
        maxCount: int ->
        projection: AgentLinkageProjection ->
        consumeOne: (HandleRecord -> Task<Result<RunCompletion, ForkError> option>) ->
        refresh: (unit -> AgentLinkageProjection) ->
            Task<Result<RunCompletion list, ForkError>>

    val reconcileFalseAborts: durable: AgentJournal -> parentId: SessionId -> Task<Result<unit, ForkError>>

    val drainFromJournalWhere:
        durable: AgentJournal ->
        parentId: SessionId ->
        maxCount: int ->
        completedAt: DateTimeOffset ->
        accept: (HandleRecord -> bool) ->
            Task<Result<RunCompletion list, ForkError>>
