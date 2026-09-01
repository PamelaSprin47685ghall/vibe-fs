namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity

module SharedAgentJournal =
    val acquire:
        directory: string ->
        processId: int ->
        startedAt: DateTimeOffset ->
        openJournal: (RuntimeId -> int -> DateTimeOffset -> Task<Result<AgentJournal, FoldRejection>>) ->
        Task<Result<AgentJournal, FoldRejection>>

    val releaseAsync: journal: AgentJournal option -> Task
    val release: journal: AgentJournal option -> unit
