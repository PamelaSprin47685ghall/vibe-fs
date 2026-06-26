module Wanxiangshu.Kernel.KnowledgeGraph.Idempotency

open Wanxiangshu.Kernel.Messaging

let returnBookkeeperToolName = "return_bookkeeper"

let historyHasCompletedReturnBookkeeper (messages: Message<'raw> list) : bool =
    messages
    |> List.exists (fun msg ->
        msg.parts
        |> List.exists (fun part ->
            match part with
            | ToolPart(toolName, _, Some state, _) ->
                toolName = returnBookkeeperToolName && state.status = "completed"
            | _ -> false))

let rejectSecondReturnBookkeeperMessage =
    "This session already completed return_bookkeeper. The knowledge graph job was submitted and persisted. "
    + "Do not call return_bookkeeper again. A second call performs no writes and only wastes context. "
    + "You were instructed to submit exactly one return_bookkeeper call. Follow the prompt."
