namespace Wanxiangshu.Next.Journal

open System
open System.Globalization
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal.FactSubEncoders

module FactDecodersPrompt =

    let decodePromptRequested (node: JsonNode) : Result<Fact, string> =
        match
            FactDecodersHelpers.tryStr "promptKey" node,
            FactDecodersHelpers.tryStr "turnId" node,
            FactDecodersHelpers.tryStr "purpose" node
        with
        | Ok pk, Ok tIdStr, Ok purp ->
            Ok(
                Prompt(
                    PromptRequested
                        {| PromptKey = pk
                           TurnId = TurnId.create tIdStr
                           Purpose = purp |}
                )
            )
        | res1, res2, res3 ->
            let err =
                match res1, res2, res3 with
                | Error e, _, _
                | _, Error e, _
                | _, _, Error e -> e
                | _ -> "Invalid PromptRequested node"

            Error err

    let decodePromptSubmitted (node: JsonNode) : Result<Fact, string> =
        match FactDecodersHelpers.tryStr "promptKey" node, FactDecodersHelpers.tryStr "messageId" node with
        | Ok pk, Ok mIdStr ->
            Ok(
                Prompt(
                    PromptSubmitted
                        {| PromptKey = pk
                           MessageId = MessageId.create mIdStr |}
                )
            )
        | res1, res2 ->
            let err =
                match res1, res2 with
                | Error e, _
                | _, Error e -> e
                | _ -> "Invalid PromptSubmitted node"

            Error err

    let decodePromptTerminal (node: JsonNode) : Result<Fact, string> =
        match FactDecodersHelpers.tryStr "promptKey" node with
        | Error err -> Error err
        | Ok pk ->
            match FactDecodersHelpers.tryGetProperty "outcome" node with
            | None -> Error "Missing required property 'outcome' for PromptTerminal"
            | Some outcomeNode ->
                match decodePromptOutcome outcomeNode with
                | Ok outcome ->
                    let msgIdOpt =
                        match FactDecodersHelpers.tryStr "assistantMessageId" node with
                        | Ok idStr -> Some(MessageId.create idStr)
                        | Error _ -> None

                    Ok(
                        Prompt(
                            PromptTerminal
                                {| PromptKey = pk
                                   Outcome = outcome
                                   AssistantMessageId = msgIdOpt |}
                        )
                    )
                | Error err -> Error err

    let decodePromptFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "PromptRequested" -> Some(decodePromptRequested node)
        | "PromptSubmitted" -> Some(decodePromptSubmitted node)
        | "PromptTerminal" -> Some(decodePromptTerminal node)
        | _ -> None
