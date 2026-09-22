namespace Wanxiangshu.Sphinx

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

type InquiryRuntime =
    new: store: IEventStore -> InquiryRuntime

    member Run:
        invocationId: string *
        question: string *
        observe: (obj -> Task<Result<obj, string>>) *
        isCancelled: (unit -> bool) *
        ?expectTurns: int *
        ?budgetRoot: string ->
            Task<obj>
