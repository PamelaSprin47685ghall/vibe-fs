namespace Wanxiangshu.Execution.Delegation

open System
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

type HandleJournalResource =
    internal new: journal: AgentJournal -> HandleJournalResource
    member internal Journal: AgentJournal
    interface IDisposable

module JournalSurface =
    val openJournal: commonDir: string -> runtimeId: string -> processId: int -> startedAt: string -> Task<obj>
    val dispose: resource: HandleJournalResource -> unit

    val link:
        resource: HandleJournalResource ->
        parentId: string ->
        agentId: string ->
        childId: string ->
        targetAgent: string ->
        roleName: string ->
            Task<obj>

    val recordAbandon:
        resource: HandleJournalResource ->
        parentId: string ->
        agentId: string ->
        reasonName: string ->
        abandonedAt: string ->
            Task<obj>

    val snapshot: resource: HandleJournalResource -> parentId: string -> handle: obj -> obj
