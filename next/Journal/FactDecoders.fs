namespace Wanxiangshu.Next.Journal

open System
open System.Globalization
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal.FactSubEncoders
open Wanxiangshu.Next.Journal.FactDecodersHelpers
open Wanxiangshu.Next.Journal.FactDecodersPrompt

module FactDecoders =

    let tryGetProperty = FactDecodersHelpers.tryGetProperty
    let tryStr = FactDecodersHelpers.tryStr
    let tryInt = FactDecodersHelpers.tryInt

    let private decodeRuntimeStarted (node: JsonNode) : Result<Fact, string> =
        match tryStr "runtimeId" node, tryInt "processId" node, tryStr "startedAt" node with
        | Ok rIdStr, Ok pId, Ok sAtStr ->
            try
                let rId = RuntimeId.create rIdStr

                match
                    DateTimeOffset.TryParseExact(
                        sAtStr,
                        "o",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind
                    )
                with
                | true, sAt ->
                    Ok(
                        Runtime(
                            RuntimeStarted
                                {| RuntimeId = rId
                                   ProcessId = pId
                                   StartedAt = sAt |}
                        )
                    )
                | false, _ -> Error $"Invalid startedAt format: {sAtStr}"
            with ex ->
                Error $"Failed parsing RuntimeStarted: {ex.Message}"
        | res1, res2, res3 ->
            let err =
                match res1, res2, res3 with
                | Error e, _, _
                | _, Error e, _
                | _, _, Error e -> e
                | _ -> "Invalid RuntimeStarted node"

            Error $"Invalid RuntimeStarted node: {err}"

    let private decodeSessionSettled (node: JsonNode) : Result<Fact, string> =
        match tryGetProperty "result" node with
        | Some resNode ->
            match decodeSessionResult resNode with
            | Ok res -> Ok(Session(SessionSettled {| Result = res |}))
            | Error err -> Error err
        | None -> Error "Missing required property 'result' for SessionSettled"

    let private decodeTodoChanged (node: JsonNode) : Result<Fact, string> =
        match tryGetProperty "snapshot" node with
        | Some snapNode ->
            match decodeTodoSnapshot snapNode with
            | Ok snap -> Ok(Todo(TodoChanged {| Snapshot = snap |}))
            | Error err -> Error err
        | None -> Error "Missing required property 'snapshot' for TodoChanged"

    let decodeFactPart1 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "RuntimeStarted" -> Some(decodeRuntimeStarted node)
        | "HumanTurnStarted" ->
            match tryStr "turnId" node with
            | Ok tIdStr -> Some(Ok(Session(HumanTurnStarted {| TurnId = TurnId.create tIdStr |})))
            | Error err -> Some(Error err)
        | "SessionSettled" -> Some(decodeSessionSettled node)
        | "TodoChanged" -> Some(decodeTodoChanged node)
        | _ -> None

    let private decodeReviewApplied (node: JsonNode) : Result<Fact, string> =
        match tryGetProperty "verdict" node, tryInt "round" node with
        | None, _ -> Error "Missing required property 'verdict' for ReviewApplied"
        | _, Error err -> Error err
        | Some verdictNode, Ok round ->
            match decodeReviewVerdict verdictNode with
            | Error err -> Error err
            | Ok verdict ->
                match tryGetProperty "resultingTodo" node with
                | Some todoNode when not (obj.ReferenceEquals(todoNode, null)) ->
                    match decodeTodoSnapshot todoNode with
                    | Ok snap ->
                        Ok(
                            Review(
                                ReviewApplied
                                    {| Verdict = verdict
                                       Round = round
                                       ResultingTodo = Some snap |}
                            )
                        )
                    | Error err -> Error err
                | _ ->
                    Ok(
                        Review(
                            ReviewApplied
                                {| Verdict = verdict
                                   Round = round
                                   ResultingTodo = None |}
                        )
                    )

    let decodeFactPart2 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match decodePromptFact tag node with
        | Some res -> Some res
        | None ->
            match tag with
            | "ReviewApplied" -> Some(decodeReviewApplied node)
            | _ -> None
