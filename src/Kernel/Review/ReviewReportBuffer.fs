module Wanxiangshu.Kernel.Review.ReviewReportBuffer

/// O(1) accumulated review report buffer: stores combined text + count,
/// NOT a growing list. The event log is the durable SSOT for individual
/// WIP report entries; this is just a live projection.
type ReviewReportBuffer = { CombinedText: string; Count: int }

/// Normalize report text: trim whitespace.
let private normalize (text: string) : string = text.Trim()

/// Empty buffer.
let empty: ReviewReportBuffer = { CombinedText = ""; Count = 0 }

/// Append a WIP report to the buffer. Empty reports are silently ignored.
let append (report: string) (state: ReviewReportBuffer) : ReviewReportBuffer =
    let trimmed = normalize report

    if trimmed = "" then
        state
    else
        let nextCount = state.Count + 1
        let section = $"## Progress Report {nextCount}\n\n{trimmed}"

        { CombinedText =
            if state.CombinedText = "" then
                section
            else
                state.CombinedText + "\n\n---\n\n" + section
          Count = nextCount }

/// Combine existing buffer with a final report text, producing the complete
/// submission report that the reviewer will see.
let withFinalReport (finalReport: string) (state: ReviewReportBuffer) : string =
    let finalText = "## Final Report\n\n" + normalize finalReport

    if state.CombinedText = "" then
        finalText
    else
        state.CombinedText + "\n\n---\n\n" + finalText
