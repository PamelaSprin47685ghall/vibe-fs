module Wanxiangshu.Shell.SubsessionTranscript

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackMessageCodec

/// Find the index of the last user message (turn boundary heuristic).
let private lastUserMessageIndex (msgs: obj array) : int option =
    msgs
    |> Array.tryFindIndexBack (fun msg ->
        let info = Dyn.get msg "info"

        if Dyn.isNullish info then
            false
        else
            Dyn.str info "role" = "user")

/// Find the index of the anchor message (user message with given ID).
let private findAnchorIndex (msgs: obj array) (anchor: TurnAnchor) : Result<int, TranscriptReadFailure> =
    match anchor with
    | AnchorByUserMessageId messageId ->
        msgs
        |> Array.tryFindIndexBack (fun msg ->
            let info = Dyn.get msg "info"
            let id = Dyn.str msg "id"
            let role = if Dyn.isNullish info then "" else Dyn.str info "role"
            (id = messageId || Dyn.str info "id" = messageId) && role = "user")
        |> Option.map Ok
        |> Option.defaultValue (
            Error { TranscriptReadFailure.Message = sprintf "Anchor user message %s not found in transcript" messageId }
        )
    | AnchorByHostRunId runId ->
        msgs
        |> Array.tryFindIndexBack (fun msg ->
            let runId1 = Dyn.str msg "runId"
            let runId2 = Dyn.str msg "runID"
            let info = Dyn.get msg "info"
            let infoRunId1 = if Dyn.isNullish info then "" else Dyn.str info "runId"
            let infoRunId2 = if Dyn.isNullish info then "" else Dyn.str info "runID"
            runId1 = runId || runId2 = runId || infoRunId1 = runId || infoRunId2 = runId)
        |> Option.map Ok
        |> Option.defaultValue (
            Error { TranscriptReadFailure.Message = sprintf "Anchor message with host run ID %s not found" runId }
        )
    | AnchorByTurnMarkerOnly ->
        match lastUserMessageIndex msgs with
        | Some idx -> Ok idx
        | None -> Error { TranscriptReadFailure.Message = "No user message found to anchor turn marker" }

/// Build CurrentTurnEvidence from messages sliced after the anchor.
/// Only analyzes messages at or after the anchor index.
let buildTurnEvidence (msgs: obj array) (anchor: TurnAnchor) : Result<CurrentTurnEvidence, TranscriptReadFailure> =
    if isNull msgs || msgs.Length = 0 then
        Ok CurrentTurnEvidence.empty
    else
        match findAnchorIndex msgs anchor with
        | Error err -> Error err
        | Ok anchorIdx ->
            let slice = msgs.[anchorIdx..]

            let todos =
                if allTodosCompleted slice then
                    TodosCompleted
                else
                    TodosNotCompleted

            let recovery =
                match scanToolCallAsText slice with
                | Some prompt -> RecoveryPrompt prompt
                | None -> NoRecoveryPrompt

            let assistant =
                match lastAssistantIndex slice with
                | None -> NoAssistant
                | Some idx ->
                    let assistantMsg = slice.[idx]
                    let parts = Dyn.get assistantMsg "parts"

                    let text =
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

                    if text = "" && not (isLastAssistantToolFinish slice) then
                        EmptyAssistant
                    else
                        let toolFinish = isLastAssistantToolFinish slice
                        AssistantContent(text, Some(if toolFinish then ToolFinish else NormalFinish))

            let tool =
                if hasToolResultAfter slice then
                    HasToolResult
                else
                    NoToolResult

            Ok
                { Assistant = assistant
                  Todos = todos
                  Tool = tool
                  Recovery = recovery }
