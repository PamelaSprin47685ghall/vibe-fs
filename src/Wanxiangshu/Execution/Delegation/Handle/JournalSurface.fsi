namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

module JournalSurface =
    val recordAbandon:
        handle: JournalHandle ->
        parent: string ->
        agent: string ->
        child: string ->
        root: string ->
        reasonName: string ->
            Task<obj>

    val snapshot: handle: JournalHandle -> parent: string -> agent: string -> child: string -> root: string -> obj
