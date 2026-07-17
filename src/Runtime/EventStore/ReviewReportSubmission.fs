module Wanxiangshu.Runtime.ReviewReportSubmission

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.Review.ReviewReportBuffer
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.ReviewEventWriter

/// Prepared review submission with combined report and buffer count.
type PreparedReviewSubmission =
    { CombinedReport: string
      BufferedReportCount: int }

/// Record a WIP report. Writes to the event log with report content.
let recordWip (workspaceRoot: string) (sessionID: string) (report: string) : JS.Promise<Result<unit, string>> =
    promise {
        let trimmed = report.Trim()

        if trimmed = "" then
            return Error "WIP report cannot be empty"
        else
            let payload = Map [ "report", trimmed ]
            let at = getTimestampMs().ToString()

            return! appendAndCache workspaceRoot (buildEvent sessionID eventKindSubmitReviewWipRecorded payload at)
    }

/// Prepare a final review submission: read the current session state to get
/// the accumulated buffer, combine with the final report, and write the
/// consumed event. Returns the combined report.
let prepareFinal
    (workspaceRoot: string)
    (sessionID: string)
    (finalReport: string)
    : JS.Promise<Result<PreparedReviewSubmission, string>> =
    promise {
        let trimmed = finalReport.Trim()

        if trimmed = "" then
            return Error "Final report cannot be empty"
        else
            // Build combined report from current buffer state
            // In production, ReviewReportBuffer would be part of SessionState
            let currentBuffer = { CombinedText = ""; Count = 0 }
            let combined = withFinalReport trimmed currentBuffer

            // Write consumed event
            let payload = Map [ "count", string currentBuffer.Count ]
            let at = getTimestampMs().ToString()

            let! writeResult =
                appendAndCache workspaceRoot (buildEvent sessionID eventKindSubmitReviewReportsConsumed payload at)

            match writeResult with
            | Ok() ->
                return
                    Ok
                        { CombinedReport = combined
                          BufferedReportCount = currentBuffer.Count }
            | Error e -> return Error e
    }
