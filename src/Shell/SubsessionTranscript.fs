module Wanxiangshu.Shell.SubsessionTranscript

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackMessageCodec

/// Build a typed TranscriptSnapshot from raw host messages.
let buildTranscriptSnapshot (msgs: obj array) : TranscriptSnapshot =
    let lastText =
        match lastAssistantIndex msgs with
        | None -> ""
        | Some idx ->
            let parts = Dyn.get msgs.[idx] "parts"

            if not (Dyn.isArray parts) then
                ""
            else
                (parts :?> obj array)
                |> Array.choose (fun p ->
                    if Dyn.str p "type" = "text" then
                        let t = Dyn.str p "text"
                        if t <> "" then Some t else None
                    else
                        None)
                |> String.concat "\n"

    { AllTodosCompleted = allTodosCompleted msgs
      ToolCallAsTextRecoveryPrompt = scanToolCallAsText msgs
      LastAssistantToolFinish = isLastAssistantToolFinish msgs
      HasToolResultAfterLastAssistant = hasToolResultAfter msgs
      LastAssistantText = lastText }
