namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Kernel.Identity

/// Frontier / request-range bounded LWR helpers for Magic Todo (protocol §18 / §30.2).
///
/// Reuses canonical LifecycleWorkRecord renderer — does NOT invent a second work-
/// record projection. Speculative / unwired; callers still own blob store + Journal.
module MagicTodoLwr =

    /// Exclusive range on XTrace for one LWR materialization.
    type BoundedRange =
        {
            /// Inclusive start cursor (often WorkRecordStart / ReviewWorkStartCursor).
            StartInclusive: XTraceCursor
            /// Exclusive end frontier (ReviewFrontier / ReviewerRecordFrontier).
            EndExclusive: XTraceCursor
        }

    /// ManagerCheckpointLWR(k): Opening excluded; range = WorkRecordStart .. ReviewFrontier(k).
    let managerCheckpointRange (workRecordStart: XTraceCursor) (reviewFrontier: XTraceCursor) : BoundedRange =
        { StartInclusive = workRecordStart
          EndExclusive = reviewFrontier }

    /// ProcessReviewLWR(k): ReviewWorkStartCursor .. ReviewerRecordFrontier
    /// (excludes assignment prompt itself and prior R history).
    let processReviewRange (reviewWorkStart: XTraceCursor) (reviewerRecordFrontier: XTraceCursor) : BoundedRange =
        { StartInclusive = reviewWorkStart
          EndExclusive = reviewerRecordFrontier }

    /// Finality dedicated LWR: FinalityReviewWorkStartCursor .. VerdictFrontier.
    let finalityReviewRange (finalityWorkStart: XTraceCursor) (verdictFrontier: XTraceCursor) : BoundedRange =
        { StartInclusive = finalityWorkStart
          EndExclusive = verdictFrontier }

    /// Slice XTrace to a bounded range (cursor ≥ start ∧ cursor < endExclusive).
    let sliceTrace (range: BoundedRange) (trace: XTraceItem list) : XTraceItem list =
        XTrace.sliceBetween range.StartInclusive range.EndExclusive trace

    /// Materialize a frontier-bounded LWR with includeOpening=false.
    ///
    /// `opening` is still required for LifecycleWorkRecord shape but is NOT rendered
    /// (process-review / Finality dedicated reports pass OpeningRaw separately).
    /// `frames` = Y compressed middle covering into the range; uncovered suffix
    /// becomes canonical RawGap via coverage vs range end.
    ///
    /// PrefixCoverage must NOT be derived from this — RawGap may be present.
    let materializeBounded
        (opening: OpeningPromptRaw)
        (frames: string list)
        (fullTrace: XTraceItem list)
        (coverage: RecordCoverage)
        (range: BoundedRange)
        (terminalItems: XTraceItem list)
        : string =
        let boundedTrace = sliceTrace range fullTrace

        // Gap start inside the bounded window: max(coverage, range start).
        // openingEnd for materialize = range start (Opening already excluded from render).
        let coverageClamped =
            if coverage.IngestedThrough.Sequence < range.StartInclusive.Sequence then
                { IngestedThrough = range.StartInclusive }
            elif coverage.IngestedThrough.Sequence > range.EndExclusive.Sequence then
                { IngestedThrough = range.EndExclusive }
            else
                coverage

        let terminalsInRange =
            terminalItems
            |> List.filter (fun item ->
                item.Cursor.Sequence >= range.StartInclusive.Sequence
                && item.Cursor.Sequence < range.EndExclusive.Sequence)

        LifecycleWorkRecord.materialize
            opening
            frames
            boundedTrace
            coverageClamped
            range.StartInclusive
            terminalsInRange
            (* includeOpening = *) false

    /// Fail closed if range is inverted or empty-as-error is required by caller.
    let validateRange (range: BoundedRange) : bool =
        range.EndExclusive.Sequence >= range.StartInclusive.Sequence
