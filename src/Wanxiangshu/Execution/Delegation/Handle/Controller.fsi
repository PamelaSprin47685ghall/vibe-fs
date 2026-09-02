namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

type HandleConsumeRejection =
    | AlreadyRetired
    | NotJoinable of HandleTransitionRejection
    | AppendFailed of string

module HandleController =
    val agentHandle: agentId: string -> HandleId

    val linkNamed:
        journal: AgentJournal option ->
        parentId: SessionId ->
        agentId: string ->
        childSessionId: SessionId ->
        targetAgent: string ->
        byname: string ->
        role: Role ->
        ownership: HandleOwnership ->
            Task<Result<unit, string>>

    val link:
        journal: AgentJournal option ->
        parentId: SessionId ->
        agentId: string ->
        childSessionId: SessionId ->
        targetAgent: string ->
        role: Role ->
        ownership: HandleOwnership ->
            Task<Result<unit, string>>

    val recordCompletion:
        journal: AgentJournal option ->
        parentId: SessionId ->
        completion: JoinableCompletion ->
            Task<Result<unit, string>>

    val recordAbandon:
        journal: AgentJournal option ->
        parentId: SessionId ->
        agentId: string ->
        reason: HandleAbandonReason ->
        abandonedAt: DateTimeOffset ->
            Task<Result<unit, string>>

    val retire: journal: AgentJournal option -> parentId: SessionId -> agentId: string -> Task<Result<unit, string>>

    val consume:
        journal: AgentJournal ->
        parentId: SessionId ->
        handle: HandleId ->
            Task<Result<HandleRecord, HandleConsumeRejection>>

    val cancelChildren:
        journal: AgentJournal option ->
        parentId: SessionId ->
        agentIds: string list ->
        abandonedAt: DateTimeOffset ->
            Task<Result<unit, string>>
