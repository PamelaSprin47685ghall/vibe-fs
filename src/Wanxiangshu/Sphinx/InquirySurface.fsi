namespace Wanxiangshu.Sphinx

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

module InquirySurface =
    val createRuntime: store: EventStoreHandle -> obj

    val run:
        runtime: obj ->
        invocationId: string ->
        question: string ->
        observe: (obj -> Task<obj>) ->
        isCancelled: (unit -> bool) ->
            Task<obj>

    val runExpected:
        runtime: obj ->
        invocationId: string ->
        question: string ->
        expectTurns: int option ->
        budgetRoot: string option ->
        observe: (obj -> Task<obj>) ->
        isCancelled: (unit -> bool) ->
            Task<obj>
