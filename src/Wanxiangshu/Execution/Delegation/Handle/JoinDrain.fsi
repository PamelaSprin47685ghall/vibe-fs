namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity

module JoinDrain =

    val reconcileFalseAborts: durable: AgentJournalPort -> parentId: SessionId -> Task<Result<unit, ForkError>>

    val drainFromJournalWhere:
        durable: AgentJournalPort ->
        parentId: SessionId ->
        maxCount: int ->
        completedAt: DateTimeOffset ->
        accept: (HandleRecord -> bool) ->
            Task<Result<RunCompletion list, ForkError>>
