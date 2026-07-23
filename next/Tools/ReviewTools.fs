namespace Wanxiangshu.Next.Tools

open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Next.Session

module ReviewTools =

    let submitReviewTool (port: SessionCommandPort) : Tool =
        { Name = "submit_review"
          Description = "Submit review task result."
          SchemaJson = """{"type":"object","properties":{"report":{"type":"string"}},"required":["report"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let reportText =
                        try
                            let decoder = Decode.field "report" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok r -> r
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let replyRef = ref (Ok SessionCommandResult.ReviewSubmitted)
                    let cmd = SubmitReview(reportText, (fun r -> replyRef := r))
                    let! res = port.Request cmd ctx.Cancellation ctx.Deadline

                    match res with
                    | Ok SessionCommandResult.ReviewSubmitted ->
                        return
                            { Result = "Review submitted and structured fact recorded"
                              Truncated = false }
                    | Ok _ ->
                        return
                            { Result = "Review submitted"
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Failed to record review submission: %A" err
                              Truncated = false }
                } }

    let returnReviewerTool (port: SessionCommandPort) : Tool =
        { Name = "return_reviewer"
          Description = "Return verdict from reviewer."
          SchemaJson = """{"type":"object","properties":{"verdict":{"type":"string"}},"required":["verdict"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let verdictText =
                        try
                            let decoder = Decode.field "verdict" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok v -> v
                            | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let replyRef = ref (Ok SessionCommandResult.VerdictReturned)
                    let cmd = ReturnVerdict(verdictText, (fun r -> replyRef := r))
                    let! res = port.Request cmd ctx.Cancellation ctx.Deadline

                    match res with
                    | Ok SessionCommandResult.VerdictReturned ->
                        return
                            { Result = sprintf "Reviewer verdict returned: %s" verdictText
                              Truncated = false }
                    | Ok _ ->
                        return
                            { Result = sprintf "Reviewer verdict returned: %s" verdictText
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Failed to record reviewer verdict: %A" err
                              Truncated = false }
                } }
