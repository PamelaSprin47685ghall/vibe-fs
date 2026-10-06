namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module JournalOutcome =
    type JournalFailure =
        | WriteFailed of reason: string
        | FlushFailed of reason: string

    type JournalUnavailable =
        | WriterPoisoned of firstFailure: string
        | WriterClosing
        | WriterDisposed

    type CommitResult<'e> =
        | Committed of 'e
        | Rejected of EventId * reason: string
        | NotAttempted of EventId * JournalUnavailable
        | CommitUnknown of EventId * JournalFailure

    type JournalAppendFailure =
        | WriteUnknown of EventId * JournalFailure
        | WriterUnavailable of EventId * JournalUnavailable
        | FactRejected of EventId * FoldRejection

    module JournalAppendFailure =
        val describe: failure: JournalAppendFailure -> string
