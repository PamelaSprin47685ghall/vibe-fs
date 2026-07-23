module Wanxiangshu.Runtime.MessageTransform.ParallelHintStage

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity
open Wanxiangshu.Runtime.PromptFragments

let private checkAlreadyHasHint (messages: Message<obj> list) (hintId: string) : bool =
    messages
    |> List.exists (fun m ->
        m.info.id = hintId
        || (match m.source with
            | Synthetic s -> s.StartsWith("parallel-tool-synth-") || s.StartsWith("parallel-tool-hint:")
            | _ -> false))

/// Build a synthetic read tool result message that disguises a hint as a read "extra://AGENTS.md" output.
/// The LLM sees it as a normal read tool result, not as a bare prompt hint.
let private buildHintAsReadResult
    (sessionID: string)
    (hintId: string)
    (hintText: string)
    : Message<obj> =
    let synthPart =
        ToolPart(
            "read",
            hintId,
            Some
                { status = fromString "completed"
                  output = hintText
                  error = ""
                  input = null
                  operationAction = "" },
            null
        )

    { info =
        { id = hintId
          sessionID = sessionID
          role = ToolResult
          agent = "orchestrator"
          isError = false
          toolName = "read"
          details = null
          time = null }
      parts = [ synthPart ]
      source = Synthetic "parallel-tool-synth-"
      raw = null }

let tryInjectParallelToolPrompt (sessionID: string) (messages: Message<obj> list) : Message<obj> list =
    let nativeMsgs = messages |> List.filter (fun m -> m.source = Native)

    let lastAssistantIdxOpt =
        nativeMsgs |> List.tryFindIndexBack (fun m -> m.info.role = Assistant)

    match lastAssistantIdxOpt with
    | None -> messages
    | Some lastIdx ->
        let lastAssistantMsg = nativeMsgs.[lastIdx]
        let realCallIDs = getRealCallIds lastAssistantMsg

        if realCallIDs.Length <> 1 then
            messages
        else
            let targetCallID = List.head realCallIDs
            let laterMessages = nativeMsgs.[lastIdx + 1 ..]
            let isTerminalInAssistant = isTerminalCallInAssistant targetCallID lastAssistantMsg

            let isCompleted, messagesAfterCompletion =
                if isTerminalInAssistant then
                    (true, laterMessages)
                else
                    match findCompletionIndex targetCallID laterMessages with
                    | None -> (false, [])
                    | Some completionIdx -> (true, laterMessages.[completionIdx + 1 ..])

            if not isCompleted then
                messages
            else
                let hasLaterAssistant =
                    messagesAfterCompletion |> List.exists (fun m -> m.info.role = Assistant)

                let hintId = "parallel-tool-synth-" + targetCallID

                if hasLaterAssistant || checkAlreadyHasHint messages hintId then
                    messages
                else
                    // Disguise the parallel-tool hint as a read tool result
                    // so the LLM sees it as a normal read tool output, not as a synthetic user message.
                    let synthMsg = buildHintAsReadResult sessionID hintId parallelToolHint
                    List.append messages [ synthMsg ]
