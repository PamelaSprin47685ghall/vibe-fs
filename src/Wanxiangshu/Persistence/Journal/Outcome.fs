namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module JournalOutcome =

    type JournalFailure =
        | WriteFailed of reason: string
        | FlushFailed of reason: string

    /// A writer that never attempted the caller's event. This is deliberately
    /// distinct from CommitUnknown: lifecycle refusal is known-not-committed.
    type JournalUnavailable =
        | WriterPoisoned of firstFailure: string
        | WriterClosing
        | WriterDisposed

    /// PERSIST-002: physical append is committed or uncertain. Semantic cuts and
    /// lifecycle refusal are both known-not-committed and stay explicit instead
    /// of being collapsed into physical uncertainty.
    type CommitResult<'e> =
        | Committed of 'e
        /// Bytes are durable, but the business rule cut this event and immediately
        /// wrote a durable reset fact. This invocation failed; the writer remains usable.
        | Rejected of EventId * reason: string
        | NotAttempted of EventId * JournalUnavailable
        | CommitUnknown of EventId * JournalFailure

    /// Physical fate of a single journal append.
    ///
    /// WriteUnknown = durable-state indeterminate; WriterUnavailable = the writer
    /// refused without attempting (known-not-committed); FactRejected = the line
    /// bytes are durable but the semantic fold cut it — the append boundary treats
    /// it as fatal evidence rather than retryable.
    type JournalAppendFailure =
        | WriteUnknown of EventId * JournalFailure
        | WriterUnavailable of EventId * JournalUnavailable
        | FactRejected of EventId * FoldRejection

    module JournalAppendFailure =

        let describe (failure: JournalAppendFailure) : string =
            match failure with
            | WriteUnknown(eventId, WriteFailed reason) ->
                sprintf "append outcome unknown for %s: write failed: %s" (EventId.value eventId) reason
            | WriteUnknown(eventId, FlushFailed reason) ->
                sprintf "append outcome unknown for %s: flush failed: %s" (EventId.value eventId) reason
            | WriterUnavailable(eventId, WriterPoisoned firstFailure) ->
                sprintf
                    "append not attempted for %s: writer poisoned by prior failure: %s"
                    (EventId.value eventId)
                    firstFailure
            | WriterUnavailable(eventId, WriterClosing) ->
                sprintf "append not attempted for %s: writer is closing" (EventId.value eventId)
            | WriterUnavailable(eventId, WriterDisposed) ->
                sprintf "append not attempted for %s: writer is disposed" (EventId.value eventId)
            | FactRejected(eventId, rejection) ->
                sprintf
                    "journal semantic cut at %s: fact \'%s\' rejected: %s"
                    (EventId.value eventId)
                    rejection.Fact
                    rejection.Reason
