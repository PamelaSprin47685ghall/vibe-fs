module Wanxiangshu.Runtime.SubsessionTranscript

open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.Fallback.FallbackMessageDetection

let private isSyntheticUserMessage (msg: obj) : bool =
    let info = Dyn.get msg "info"

    let infoSynthetic =
        if Dyn.isNullish info then
            false
        else
            let v = Dyn.get info "synthetic"
            not (Dyn.isNullish v) && unbox<bool> v

    if infoSynthetic then
        true
    else
        let parts = Dyn.get msg "parts"

        if not (Dyn.isNullish parts) && Dyn.isArray parts then
            (parts :?> obj array)
            |> Array.exists (fun part ->
                let v = Dyn.get part "synthetic"
                not (Dyn.isNullish v) && unbox<bool> v)
        else
            false

/// Find the index of the last human user message (turn boundary heuristic).
/// Synthetic user messages are ignored because they are not human turns.
let private lastUserMessageIndex (msgs: obj array) : int option =
    msgs
    |> Array.tryFindIndexBack (fun msg ->
        let info = Dyn.get msg "info"

        if Dyn.isNullish info || isSyntheticUserMessage msg then
            false
        else
            Dyn.str info "role" = "user")

/// Find the index of the anchor message (user message with given ID).
let private findAnchorIndex (msgs: obj array) (anchor: TurnAnchor) : Result<int, TranscriptReadFailure> =
    match anchor with
    | AnchorByUserMessageId messageId ->
        match
            msgs
            |> Array.tryFindIndexBack (fun msg ->
                let info = Dyn.get msg "info"
                let id = Dyn.str msg "id"
                let role = if Dyn.isNullish info then "" else Dyn.str info "role"
                (id = messageId || Dyn.str info "id" = messageId) && role = "user")
        with
        | Some idx ->
            match lastUserMessageIndex msgs with
            | Some lastIdx when idx < lastIdx ->
                Error { TranscriptReadFailure.Message = sprintf "Anchor user message %s is stale" messageId }
            | _ -> Ok idx
        | None ->
            Error { TranscriptReadFailure.Message = sprintf "Anchor user message %s not found in transcript" messageId }
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

/// Fallback: build evidence from the latest assistant message after the last
/// user message (or full-history when no user exists). Returns None when the
/// messages array is empty, no assistant follows the last user, or the
/// trailing assistant has no non-empty text.
let tryBuildLatestAssistantEvidence (msgs: obj array) : CurrentTurnEvidence option =
    if isNull msgs || msgs.Length = 0 then
        None
    else
        let searchDomain =
            match lastUserMessageIndex msgs with
            | Some idx -> msgs.[idx..]
            | None -> msgs

        searchDomain
        |> Array.tryFindBack (fun msg ->
            let info = Dyn.get msg "info"
            not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")
        |> Option.bind (fun assistantMsg ->
            let parts = Dyn.get assistantMsg "parts"

            if not (Dyn.isArray parts) then
                None
            else
                let text =
                    (parts :?> obj array)
                    |> Array.choose (fun p ->
                        if Dyn.str p "type" = "text" then
                            let t = Dyn.str p "text"
                            if t <> "" then Some t else None
                        else
                            None)
                    |> String.concat "\n"

                if text = "" then
                    None
                else
                    Some
                        { CurrentTurnEvidence.empty with
                            Assistant = AssistantSnapshot("", 0L, text, Some NormalFinish) })

/// Build CurrentTurnEvidence from messages sliced after the anchor.
/// Only analyzes messages at or after the anchor index.
let private buildAssistantEvidence (slice: obj array) : AssistantEvidence =
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

        if text = "" then
            EmptyAssistant
        else
            let toolFinish = isLastAssistantToolFinish slice
            AssistantSnapshot("", 0L, text, Some(if toolFinish then ToolFinish else NormalFinish))

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

            let assistant = buildAssistantEvidence slice

            let tool =
                if hasToolResultAfter slice then
                    HasToolResult
                else
                    NoToolResult

            Ok
                { Assistant = assistant
                  Todos = todos
                  Tool = tool
                  Recovery = recovery
                  Outcome = NoOutcome
                  IdleObserved = false }
