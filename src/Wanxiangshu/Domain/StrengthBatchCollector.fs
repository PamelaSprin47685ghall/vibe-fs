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
        // DSL-MUTABLE: resource — scanning cursor over wire messages
        let all = List.toArray messages
        let collected = ResizeArray<StrengthRequestBatch>()
        // DSL-MUTABLE: resource
        let mutable index = 0
        // DSL-MUTABLE: resource
        let mutable requestOrdinal = 0
        // DSL-MUTABLE: resource
        let mutable stopped = 0

        while index < all.Length && stopped = 0 do
            let message = all.[index]

            if String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) then
                requestOrdinal <- requestOrdinal + 1
                let calls = callsOf message

                if List.isEmpty calls then
                    // A text/reasoning-only provider completion terminates the
                    // speculative loop; there can be no later batch in this decision.
                    stopped <- 1
                else
                    let callIds = calls |> List.map (fun call -> ToolCallId.value call.Id) |> Set.ofList
                    let results = System.Collections.Generic.Dictionary<string, string>()
                    // DSL-MUTABLE: resource
                    let mutable duplicateOrForeign = 0
                    // DSL-MUTABLE: resource
                    let mutable cursor = index + 1
                    // DSL-MUTABLE: resource
                    let mutable atBoundary = 0

                    while cursor < all.Length && atBoundary = 0 do
                        let next = all.[cursor]

                        if
                            String.Equals(next.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                            || String.Equals(next.Role, "user", StringComparison.OrdinalIgnoreCase)
                        then
                            atBoundary <- 1
                        else
                            for callId, result in resultParts next do
                                let key = ToolCallId.value callId

                                if not (Set.contains key callIds) || results.ContainsKey key then
                                    duplicateOrForeign <- 1
                                else
                                    results.[key] <- result

                            cursor <- cursor + 1

                    if duplicateOrForeign <> 0 || results.Count <> calls.Length then
                        // Preserve earlier complete batches, but never jump over an
                        // incomplete request: that would relabel provider request N+1
                        // as N and corrupt K accounting.
                        stopped <- 1
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

                    index <- if atBoundary <> 0 then cursor else max cursor (index + 1)
            else
                index <- index + 1

        collected |> Seq.toList
