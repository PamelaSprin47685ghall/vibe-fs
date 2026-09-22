namespace Wanxiangshu.Sphinx

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

module InquirySurface =
    let createRuntime (store: EventStoreHandle) : obj = box (InquiryRuntime store.Store)

    let run (runtime: obj) invocationId question (observe: obj -> Task<obj>) isCancelled =
        let observeWork work =
            task {
                try
                    let! observation = observe work
                    return Ok observation
                with error ->
                    return Error error.Message
            }

        (unbox<InquiryRuntime> runtime)
            .Run(invocationId, question, observeWork, isCancelled)

    let runExpected
        (runtime: obj)
        invocationId
        question
        (expectTurns: int option)
        (budgetRoot: string option)
        (observe: obj -> Task<obj>)
        isCancelled
        =
        let observeWork work =
            task {
                try
                    let! observation = observe work
                    return Ok observation
                with error ->
                    return Error error.Message
            }

        (unbox<InquiryRuntime> runtime)
            .Run(invocationId, question, observeWork, isCancelled, ?expectTurns = expectTurns, ?budgetRoot = budgetRoot)
