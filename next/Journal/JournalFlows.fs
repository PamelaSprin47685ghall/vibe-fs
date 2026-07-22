namespace Wanxiangshu.Next.Journal

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

[<RequireQualifiedAccess>]
type JournalError =
    | JournalCancelled
    | JournalFailed of reason: string

type JournalContext = unit

type JournalFlow<'a> = Flow<JournalContext, JournalError, 'a>

module JournalFlows =

    let journalProgress: ProgressGuard<JournalContext, JournalError> option = None

    let journal = FlowBuilder<JournalContext, JournalError>(journalProgress)

    let runFlow (ctx: JournalContext) (ct: CancellationToken) (flow: JournalFlow<'a>) : Task<Result<'a, JournalError>> =
        task {
            try
                return! Flow.run ctx ct flow
            with :? OperationCanceledException ->
                return Error JournalError.JournalCancelled
        }
