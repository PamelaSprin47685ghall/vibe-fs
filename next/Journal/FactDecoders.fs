namespace Wanxiangshu.Next.Journal

open System
open System.Globalization
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal.FactSubEncoders

module FactDecoders =

    let private tryGetProperty (name: string) (node: JsonNode) : JsonNode option =
        if obj.ReferenceEquals(node, null) then
            None
        else
            try
                let objNode = node.AsObject()

                if not (obj.ReferenceEquals(objNode, null)) && objNode.ContainsKey(name) then
                    Some objNode.[name]
                else
                    None
            with _ ->
                None

    let private tryStr (name: string) (node: JsonNode) : Result<string, string> =
        match tryGetProperty name node with
        | Some n ->
            (try
                Ok(n.GetValue<string>())
             with ex ->
                 Error(sprintf "Failed parsing string property '%s': %s" name ex.Message))
        | None -> Error(sprintf "Missing required string property '%s'" name)

    let private tryInt (name: string) (node: JsonNode) : Result<int, string> =
        match tryGetProperty name node with
        | Some n ->
            (try
                Ok(n.GetValue<int>())
             with ex ->
                 Error(sprintf "Failed parsing int property '%s': %s" name ex.Message))
        | None -> Error(sprintf "Missing required int property '%s'" name)

    let decodeFactPart1 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "RuntimeStarted" ->
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
                        Some(
                            Ok(
                                Runtime(
                                    RuntimeStarted
                                        {| RuntimeId = rId
                                           ProcessId = pId
                                           StartedAt = sAt |}
                                )
                            )
                        )
                    | false, _ -> Some(Error $"Invalid startedAt format: {sAtStr}")
                with ex ->
                    Some(Error $"Failed parsing RuntimeStarted: {ex.Message}")
            | res1, res2, res3 ->
                let err =
                    match res1, res2, res3 with
                    | Error e, _, _
                    | _, Error e, _
                    | _, _, Error e -> e
                    | _ -> "Invalid RuntimeStarted node"

                Some(Error $"Invalid RuntimeStarted node: {err}")
        | "HumanTurnStarted" ->
            match tryStr "turnId" node with
            | Ok tIdStr -> Some(Ok(Session(HumanTurnStarted {| TurnId = TurnId.create tIdStr |})))
            | Error err -> Some(Error err)
        | "SessionSettled" ->
            match tryGetProperty "result" node with
            | Some resNode ->
                (match decodeSessionResult resNode with
                 | Ok res -> Some(Ok(Session(SessionSettled {| Result = res |})))
                 | Error err -> Some(Error err))
            | None -> Some(Error "Missing required property 'result' for SessionSettled")
        | "TodoChanged" ->
            match tryGetProperty "snapshot" node with
            | Some snapNode ->
                (match decodeTodoSnapshot snapNode with
                 | Ok snap -> Some(Ok(Todo(TodoChanged {| Snapshot = snap |})))
                 | Error err -> Some(Error err))
            | None -> Some(Error "Missing required property 'snapshot' for TodoChanged")
        | _ -> None

    let private decodePromptFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "PromptRequested" ->
            match tryStr "promptKey" node, tryStr "turnId" node, tryStr "purpose" node with
            | Ok pk, Ok tIdStr, Ok purp ->
                Some(
                    Ok(
                        Prompt(
                            PromptRequested
                                {| PromptKey = pk
                                   TurnId = TurnId.create tIdStr
                                   Purpose = purp |}
                        )
                    )
                )
            | res1, res2, res3 ->
                let err =
                    match res1, res2, res3 with
                    | Error e, _, _
                    | _, Error e, _
                    | _, _, Error e -> e
                    | _ -> "Invalid PromptRequested node"

                Some(Error err)
        | "PromptSubmitted" ->
            match tryStr "promptKey" node, tryStr "messageId" node with
            | Ok pk, Ok mIdStr ->
                Some(
                    Ok(
                        Prompt(
                            PromptSubmitted
                                {| PromptKey = pk
                                   MessageId = MessageId.create mIdStr |}
                        )
                    )
                )
            | res1, res2 ->
                let err =
                    match res1, res2 with
                    | Error e, _
                    | _, Error e -> e
                    | _ -> "Invalid PromptSubmitted node"

                Some(Error err)
        | "PromptTerminal" ->
            match tryStr "promptKey" node with
            | Error err -> Some(Error err)
            | Ok pk ->
                match tryGetProperty "outcome" node with
                | None -> Some(Error "Missing required property 'outcome' for PromptTerminal")
                | Some outcomeNode ->
                    match decodePromptOutcome outcomeNode with
                    | Ok outcome ->
                        let msgIdOpt =
                            match tryStr "assistantMessageId" node with
                            | Ok idStr -> Some(MessageId.create idStr)
                            | Error _ -> None

                        Some(
                            Ok(
                                Prompt(
                                    PromptTerminal
                                        {| PromptKey = pk
                                           Outcome = outcome
                                           AssistantMessageId = msgIdOpt |}
                                )
                            )
                        )
                    | Error err -> Some(Error err)
        | _ -> None

    let decodeFactPart2 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match decodePromptFact tag node with
        | Some res -> Some res
        | None ->
            match tag with
            | "ReviewApplied" ->
                match tryGetProperty "verdict" node, tryInt "round" node with
                | None, _ -> Some(Error "Missing required property 'verdict' for ReviewApplied")
                | _, Error err -> Some(Error err)
                | Some verdictNode, Ok round ->
                    match decodeReviewVerdict verdictNode with
                    | Error err -> Some(Error err)
                    | Ok verdict ->
                        match tryGetProperty "resultingTodo" node with
                        | Some todoNode when not (obj.ReferenceEquals(todoNode, null)) ->
                            (match decodeTodoSnapshot todoNode with
                             | Ok snap ->
                                 Some(
                                     Ok(
                                         Review(
                                             ReviewApplied
                                                 {| Verdict = verdict
                                                    Round = round
                                                    ResultingTodo = Some snap |}
                                         )
                                     )
                                 )
                             | Error err -> Some(Error err))
                        | _ ->
                            Some(
                                Ok(
                                    Review(
                                        ReviewApplied
                                            {| Verdict = verdict
                                               Round = round
                                               ResultingTodo = None |}
                                    )
                                )
                            )
            | _ -> None

    let private decodeChildProcessFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "ChildCreated" ->
            match tryStr "childId" node, tryStr "targetAgent" node with
            | Ok cIdStr, Ok agent ->
                Some(
                    Ok(
                        Child(
                            ChildCreated
                                {| ChildId = ChildId.create cIdStr
                                   TargetAgent = agent |}
                        )
                    )
                )
            | res1, res2 ->
                let err =
                    match res1, res2 with
                    | Error e, _
                    | _, Error e -> e
                    | _ -> "Invalid ChildCreated node"

                Some(Error err)
        | "ChildCompletedFact" ->
            match tryStr "childId" node with
            | Error err -> Some(Error err)
            | Ok cIdStr ->
                match tryGetProperty "result" node with
                | None -> Some(Error "Missing required property 'result' for ChildCompletedFact")
                | Some resNode ->
                    (match decodeChildResult resNode with
                     | Ok res ->
                         Some(
                             Ok(
                                 Child(
                                     ChildCompletedFact
                                         {| ChildId = ChildId.create cIdStr
                                            Result = res |}
                                 )
                             )
                         )
                     | Error err -> Some(Error err))
        | "ProcessSpawned" ->
            match tryStr "processId" node, tryStr "command" node with
            | Ok pIdStr, Ok cmd ->
                Some(
                    Ok(
                        Process(
                            ProcessSpawned
                                {| ProcessId = ProcessId.create pIdStr
                                   Command = cmd |}
                        )
                    )
                )
            | res1, res2 ->
                let err =
                    match res1, res2 with
                    | Error e, _
                    | _, Error e -> e
                    | _ -> "Invalid ProcessSpawned node"

                Some(Error err)
        | "ProcessExited" ->
            match tryStr "processId" node with
            | Error err -> Some(Error err)
            | Ok pIdStr ->
                match tryGetProperty "result" node with
                | None -> Some(Error "Missing required property 'result' for ProcessExited")
                | Some resNode ->
                    (match decodeProcessResult resNode with
                     | Ok res ->
                         Some(
                             Ok(
                                 Process(
                                     ProcessExited
                                         {| ProcessId = ProcessId.create pIdStr
                                            Result = res |}
                                 )
                             )
                         )
                     | Error err -> Some(Error err))
        | _ -> None

    let private decodeSquadFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "TaskVerifiedFact" ->
            match tryStr "taskId" node with
            | Error err -> Some(Error err)
            | Ok tId ->
                match tryGetProperty "result" node with
                | None -> Some(Error "Missing required property 'result' for TaskVerifiedFact")
                | Some resNode ->
                    (match decodeSquadTaskResult resNode with
                     | Ok res -> Some(Ok(Squad(TaskVerifiedFact {| TaskId = tId; Result = res |})))
                     | Error err -> Some(Error err))
        | "WaveAccepted" ->
            match tryInt "waveIndex" node with
            | Error err -> Some(Error err)
            | Ok idx ->
                match tryGetProperty "acceptedTaskIds" node with
                | None -> Some(Error "Missing required property 'acceptedTaskIds' for WaveAccepted")
                | Some arrNode ->
                    (match getStrArray arrNode with
                     | Ok taskIds ->
                         Some(
                             Ok(
                                 Squad(
                                     WaveAccepted
                                         {| WaveIndex = idx
                                            AcceptedTaskIds = taskIds |}
                                 )
                             )
                         )
                     | Error err -> Some(Error err))
        | "FastForwardCompleted" ->
            match tryStr "taskId" node, tryStr "targetRef" node with
            | Ok tId, Ok tRef -> Some(Ok(Squad(FastForwardCompleted {| TaskId = tId; TargetRef = tRef |})))
            | res1, res2 ->
                let err =
                    match res1, res2 with
                    | Error e, _
                    | _, Error e -> e
                    | _ -> "Invalid FastForwardCompleted node"

                Some(Error err)
        | _ -> None

    let decodeFactPart3 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match decodeChildProcessFact tag node with
        | Some res -> Some res
        | None -> decodeSquadFact tag node
