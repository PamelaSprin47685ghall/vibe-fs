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

    let collectCompleteBatches (messages: ProviderProjection.WireMessage list) : StrengthRequestBatch list =
        let all = List.toArray messages
        let collected = ResizeArray<StrengthRequestBatch>()
        let mutable index = 0
        let mutable requestOrdinal = 0
        let mutable stopped = false

        while index < all.Length && not stopped do
            let message = all.[index]

            if String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) then
                requestOrdinal <- requestOrdinal + 1
                let calls = callsOf message

                if List.isEmpty calls then
                    // A text/reasoning-only provider completion terminates the
                    // speculative loop; there can be no later batch in this decision.
                    stopped <- true
                else
                    let callIds = calls |> List.map (fun call -> ToolCallId.value call.Id) |> Set.ofList
                    let results = System.Collections.Generic.Dictionary<string, string>()
                    let mutable duplicateOrForeign = false
                    let mutable cursor = index + 1
                    let mutable boundary = false

                    while cursor < all.Length && not boundary do
                        let next = all.[cursor]

                        if
                            String.Equals(next.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                            || String.Equals(next.Role, "user", StringComparison.OrdinalIgnoreCase)
                        then
                            boundary <- true
                        else
                            for callId, result in resultParts next do
                                let key = ToolCallId.value callId

                                if not (Set.contains key callIds) || results.ContainsKey key then
                                    duplicateOrForeign <- true
                                else
                                    results.[key] <- result

                            cursor <- cursor + 1

                    if duplicateOrForeign || results.Count <> calls.Length then
                        // Preserve earlier complete batches, but never jump over an
                        // incomplete request: that would relabel provider request N+1
                        // as N and corrupt K accounting.
                        stopped <- true
                    else
                        let exchanges =
                            calls
                            |> List.map (fun call ->
                                { ToolName = call.Name.Trim().ToLowerInvariant()
                                  CanonicalArguments = call.Arguments
                                  CanonicalResult = results.[ToolCallId.value call.Id] })

                        collected.Add
                            { RequestOrdinal = requestOrdinal
                              Exchanges = exchanges }

                    index <- if boundary then cursor else max cursor (index + 1)
            else
                index <- index + 1

        collected |> Seq.toList
