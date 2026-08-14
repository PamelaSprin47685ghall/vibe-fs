namespace Wanxiangshu.Strength.Prediction

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength

open System
open Wanxiangshu.Foundation.Identity

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

    let private isRequestBoundary (message: ProviderProjection.WireMessage) =
        String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
        || String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)

    let private collectResults
        (all: ProviderProjection.WireMessage array)
        (callIds: Set<string>)
        (startIndex: int)
        : Result<Map<string, string> * int, unit> =
        let rec loop index results =
            if index >= all.Length || isRequestBoundary all.[index] then
                Ok(results, index)
            else
                let nextResults =
                    resultParts all.[index]
                    |> List.fold
                        (fun state (callId, result) ->
                            state
                            |> Result.bind (fun current ->
                                let key = ToolCallId.value callId

                                if not (Set.contains key callIds) || Map.containsKey key current then
                                    Error()
                                else
                                    Ok(Map.add key result current)))
                        (Ok results)

                match nextResults with
                | Error() -> Error()
                | Ok current -> loop (index + 1) current

        loop startIndex Map.empty

    let collectCompleteBatches (messages: ProviderProjection.WireMessage list) : StrengthRequestBatch list =
        let all = List.toArray messages

        let rec loop index requestOrdinal collected =
            if index >= all.Length then
                List.rev collected
            else
                let message = all.[index]

                if not (String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)) then
                    loop (index + 1) requestOrdinal collected
                else
                    let nextOrdinal = requestOrdinal + 1
                    let calls = callsOf message

                    if List.isEmpty calls then
                        List.rev collected
                    else
                        let callIds = calls |> List.map (fun call -> ToolCallId.value call.Id) |> Set.ofList

                        match collectResults all callIds (index + 1) with
                        | Error() -> List.rev collected
                        | Ok(results, nextIndex) when Map.count results <> List.length calls -> List.rev collected
                        | Ok(results, nextIndex) ->
                            let exchanges =
                                calls
                                |> List.map (fun call ->
                                    { ToolName = call.Name.Trim().ToLowerInvariant()
                                      CanonicalArguments = call.Arguments
                                      CanonicalResult = Map.find (ToolCallId.value call.Id) results })

                            loop
                                nextIndex
                                nextOrdinal
                                ({ RequestOrdinal = nextOrdinal
                                   Exchanges = exchanges }
                                 :: collected)

        loop 0 0 []
