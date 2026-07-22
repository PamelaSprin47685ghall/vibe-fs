module Wanxiangshu.Runtime.MessageTransform.ParallelHintStage

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity
open Wanxiangshu.Runtime.PromptFragments

let private checkAlreadyHasHint (messages: Message<obj> list) (hintId: string) : bool =
    messages
    |> List.exists (fun m ->
        m.info.id = hintId
        || (match m.source with
            | Synthetic s -> s.StartsWith("parallel-tool-synth-") || s.StartsWith("parallel-tool-hint:")
            | _ -> false))

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
                    let synthMsg: Message<obj> =
                        { info =
                            { id = hintId
                              sessionID = sessionID
                              role = User
                              agent = "orchestrator"
                              isError = false
                              toolName = ""
                              details = null
                              time = null }
                          parts = [ TextPart parallelToolHint ]
                          source = Synthetic "parallel-tool-synth-"
                          raw = null }

                    List.append messages [ synthMsg ]
