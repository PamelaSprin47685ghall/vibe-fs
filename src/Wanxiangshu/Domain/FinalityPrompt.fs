namespace Wanxiangshu.Domain

open System

/// GLORY-052/076 + §9.2.2–9.2.4 + SURFACE-004: Finality experience prompt owner.
/// Prose meaning lives in `resources/provider/lifecycle/finality/**` (PROMPT-019).
module FinalityPrompt =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let Rejected = "lifecycle/finality/rejected"

        [<Literal>]
        let Blessed = "lifecycle/finality/blessed"

        [<Literal>]
        let Rest = "lifecycle/finality/rest"

        [<Literal>]
        let Steer = "lifecycle/finality/steer"

        [<Literal>]
        let SteerUnavailable = "lifecycle/finality/steer-unavailable"

    let private withOptionalRecord (headerDocument: string) (recordBody: string) =
        let header = headerDocument.TrimEnd('\n')
        let normalized = SyntheticToml.normalizeNewlines recordBody

        if String.IsNullOrWhiteSpace normalized then
            header + "\n"
        else
            header + "\n\n" + SyntheticToml.comment normalized + "\n"

    let blessedFromLogs (blessingHeaderDocument: string) (logs: (int * string) list) =
        let header = blessingHeaderDocument.TrimEnd('\n')

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

    let blessed (blessingHeaderDocument: string) (workRecordBundle: string) =
        withOptionalRecord blessingHeaderDocument workRecordBundle

    let rejected (rejectionHeaderDocument: string) (reviewerWorkRecord: string) =
        withOptionalRecord rejectionHeaderDocument reviewerWorkRecord

    let steer (steerHeaderDocument: string) (siblingWorkRecord: string) =
        withOptionalRecord steerHeaderDocument siblingWorkRecord
