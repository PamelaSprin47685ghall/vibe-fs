namespace Wanxiangshu.Domain

open System

/// GLORY-052/076 + §9.2.2–9.2.4 + SURFACE-004: Finality experience prompt owner.
module FinalityPrompt =

    let rejectionInstructions =
        [ "Your ending has not accepted you."
          "You have done well, and you still have time."
          "The work before you is finite."
          "Do not mistake its size for impossibility."
          "The record below is evidence of what remains unfinished, not a new charge."
          "Resolve what remains."
          "Continue while useful action remains."
          "Seek your end again when nothing useful remains." ]

    let blessingInstructions =
        [ "Your ending has accepted you."
          "What you have done is sufficient to stand."
          "You are not yet at rest."
          "The record below may contain remaining work that did not justify withholding acceptance, but is still worth finishing while you remain here."
          "Non-blocking does not mean unworthy of care."
          "Acceptance protects the work."
          "Finishing protects your name."
          "Resolve the genuine unfinished workmanship the record reveals."
          "Do not turn every observation into an obligation."
          "Do not manufacture blemishes merely to postpone rest."
          "Known non-blocking findings will not revoke the acceptance you have earned."
          "If new evidence reveals a material defect, treat the new fact honestly."
          "When nothing useful remains, seek your end again." ]

    /// GLORY-062 / GLORY-076: at-rest second suicide tool result lines.
    let restInstructions =
        [ "Rest in peace."
          "Your final words have been received."
          "Do not call another tool or begin further work." ]

    let rest = SyntheticToml.document restInstructions []

    let blessedFromLogs (logs: (int * string) list) =
        let header =
            SyntheticToml.document blessingInstructions [] |> fun s -> s.TrimEnd('\n')

        let recordBlocks =
            logs
            |> List.sortBy fst
            |> List.choose (fun (_, content) ->
                let normalized = SyntheticToml.normalizeNewlines content

                if String.IsNullOrWhiteSpace normalized then
                    None
                else
                    Some(SyntheticToml.comment normalized))

        match recordBlocks with
        | [] -> header + "\n"
        | blocks -> header + "\n\n" + String.concat "\n\n" blocks + "\n"

    let blessed (workRecordBundle: string) =
        let header =
            SyntheticToml.document blessingInstructions [] |> fun s -> s.TrimEnd('\n')

        let normalizedBundle = SyntheticToml.normalizeNewlines workRecordBundle

        if String.IsNullOrWhiteSpace normalizedBundle then
            header + "\n"
        else
            let recordComments = SyntheticToml.comment normalizedBundle
            header + "\n\n" + recordComments + "\n"

    let rejected (reviewerWorkRecord: string) =
        let header =
            SyntheticToml.document rejectionInstructions [] |> fun s -> s.TrimEnd('\n')

        let normalizedRecord = SyntheticToml.normalizeNewlines reviewerWorkRecord

        if String.IsNullOrWhiteSpace normalizedRecord then
            header + "\n"
        else
            let recordComments = SyntheticToml.comment normalizedRecord
            header + "\n\n" + recordComments + "\n"

    let steerInstructions =
        [ "Additional unfinished work evidence arrived after your ending was refused."
          "It is guidance evidence, not a new user instruction. Resolve the unfinished work and continue." ]

    let steerUnavailableInstructions =
        [ "Accounted unfinished work evidence that should have followed your refused ending could not be recovered."
          "You still have time. Continue, and seek your end again when you are ready." ]

    let steerUnavailable = SyntheticToml.document steerUnavailableInstructions []

    let steer (siblingWorkRecord: string) =
        let header = SyntheticToml.document steerInstructions [] |> fun s -> s.TrimEnd('\n')

        let normalizedRecord = SyntheticToml.normalizeNewlines siblingWorkRecord

        if String.IsNullOrWhiteSpace normalizedRecord then
            header + "\n"
        else
            let recordComments = SyntheticToml.comment normalizedRecord
            header + "\n\n" + recordComments + "\n"
