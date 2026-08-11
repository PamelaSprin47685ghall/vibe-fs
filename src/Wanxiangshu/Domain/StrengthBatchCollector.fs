namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel.Identity

/// STRENGTH-003/005: recover completed Replica provider-request batches from the
/// Host wire transcript. Request boundaries come from assistant messages, never
/// tool-call count. A batch is emitted only when every call has exactly one result
/// before the next provider/user message; result arrival order is normalized back
/// to provider call order.
[<RequireQualifiedAccess>]
module StrengthBatchCollector =

    type private Call =
        { Id: ToolCallId
          Name: string
          Arguments: string }

    let private callsOf (message: ProviderProjection.WireMessage) =
        message.Parts
        |> List.choose (function
            | ProviderProjection.WireToolCall(callId, name, arguments) ->
                Some
                    { Id = callId
                      Name = name
                      Arguments = arguments }
            | _ -> None)

    let private resultParts (message: ProviderProjection.WireMessage) =
        message.Parts
        |> List.choose (function
            | ProviderProjection.WireToolResult(callId, result) -> Some(callId, result)
            | _ -> None)

    let private isBoundary (message: ProviderProjection.WireMessage) =
        String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
        || String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)

    let private collectResults (boundarySlice: ProviderProjection.WireMessage list) =
        boundarySlice
        |> List.collect resultParts
        |> List.fold
            (fun (valid, acc: Map<string, string>) (callId, result) ->
                let key = ToolCallId.value callId

                if not valid || Map.containsKey key acc then
                    false, acc
                else
                    true, Map.add key result acc)
            (true, Map.empty)

    let collectCompleteBatches (messages: ProviderProjection.WireMessage list) : StrengthRequestBatch list =
        let rec loop (remaining: ProviderProjection.WireMessage list) ordinal acc =
            match remaining with
            | [] -> List.rev acc
            | message :: tail ->
                if String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) then
                    let calls = callsOf message

                    if List.isEmpty calls then
                        List.rev acc
                    else
                        let callIdSet = calls |> List.map (fun c -> ToolCallId.value c.Id) |> Set.ofList
                        let toolMessages = tail |> List.takeWhile (not << isBoundary)
                        let rest = tail |> List.skipWhile (not << isBoundary)

                        let noForeignCalls =
                            toolMessages
                            |> List.collect resultParts
                            |> List.forall (fun (id, _) -> Set.contains (ToolCallId.value id) callIdSet)

                        let noDuplicates, resultMap = collectResults toolMessages

                        if noForeignCalls && noDuplicates && Map.count resultMap = List.length calls then
                            let exchanges =
                                calls
                                |> List.map (fun call ->
                                    { ToolName = call.Name.Trim().ToLowerInvariant()
                                      CanonicalArguments = call.Arguments
                                      CanonicalResult = resultMap.[ToolCallId.value call.Id] })

                            let batch =
                                { RequestOrdinal = ordinal
                                  Exchanges = exchanges }

                            loop rest (ordinal + 1) (batch :: acc)
                        else
                            List.rev acc
                else
                    loop tail ordinal acc

        loop messages 1 []
