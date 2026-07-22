namespace Wanxiangshu.Next.Journal

open System
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal.FactSubEncoders
open Wanxiangshu.Next.Journal.FactDecodersHelpers

module FactDecodersExtras =

    let private decodeChildCreated (node: JsonNode) : Result<Fact, string> =
        match tryStr "childId" node, tryStr "targetAgent" node with
        | Ok cIdStr, Ok agent ->
            Ok(
                Child(
                    ChildCreated
                        {| ChildId = ChildId.create cIdStr
                           TargetAgent = agent |}
                )
            )
        | res1, res2 ->
            let err =
                match res1, res2 with
                | Error e, _
                | _, Error e -> e
                | _ -> "Invalid ChildCreated node"

            Error err

    let private decodeChildCompletedFact (node: JsonNode) : Result<Fact, string> =
        match tryStr "childId" node with
        | Error err -> Error err
        | Ok cIdStr ->
            match tryGetProperty "result" node with
            | None -> Error "Missing required property 'result' for ChildCompletedFact"
            | Some resNode ->
                match decodeChildResult resNode with
                | Ok res ->
                    Ok(
                        Child(
                            ChildCompletedFact
                                {| ChildId = ChildId.create cIdStr
                                   Result = res |}
                        )
                    )
                | Error err -> Error err

    let private decodeProcessSpawned (node: JsonNode) : Result<Fact, string> =
        match tryStr "processId" node, tryStr "command" node with
        | Ok pIdStr, Ok cmd ->
            Ok(
                Process(
                    ProcessSpawned
                        {| ProcessId = ProcessId.create pIdStr
                           Command = cmd |}
                )
            )
        | res1, res2 ->
            let err =
                match res1, res2 with
                | Error e, _
                | _, Error e -> e
                | _ -> "Invalid ProcessSpawned node"

            Error err

    let private decodeProcessExited (node: JsonNode) : Result<Fact, string> =
        match tryStr "processId" node with
        | Error err -> Error err
        | Ok pIdStr ->
            match tryGetProperty "result" node with
            | None -> Error "Missing required property 'result' for ProcessExited"
            | Some resNode ->
                match decodeProcessResult resNode with
                | Ok res ->
                    Ok(
                        Process(
                            ProcessExited
                                {| ProcessId = ProcessId.create pIdStr
                                   Result = res |}
                        )
                    )
                | Error err -> Error err

    let private decodeChildProcessFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "ChildCreated" -> Some(decodeChildCreated node)
        | "ChildCompletedFact" -> Some(decodeChildCompletedFact node)
        | "ProcessSpawned" -> Some(decodeProcessSpawned node)
        | "ProcessExited" -> Some(decodeProcessExited node)
        | _ -> None

    let private decodeTaskVerifiedFact (node: JsonNode) : Result<Fact, string> =
        match tryStr "taskId" node with
        | Error err -> Error err
        | Ok tId ->
            match tryGetProperty "result" node with
            | None -> Error "Missing required property 'result' for TaskVerifiedFact"
            | Some resNode ->
                match decodeSquadTaskResult resNode with
                | Ok res -> Ok(Squad(TaskVerifiedFact {| TaskId = tId; Result = res |}))
                | Error err -> Error err

    let private decodeWaveAccepted (node: JsonNode) : Result<Fact, string> =
        match tryInt "waveIndex" node with
        | Error err -> Error err
        | Ok idx ->
            match tryGetProperty "acceptedTaskIds" node with
            | None -> Error "Missing required property 'acceptedTaskIds' for WaveAccepted"
            | Some arrNode ->
                match getStrArray arrNode with
                | Ok taskIds ->
                    Ok(
                        Squad(
                            WaveAccepted
                                {| WaveIndex = idx
                                   AcceptedTaskIds = taskIds |}
                        )
                    )
                | Error err -> Error err

    let private decodeFastForwardCompleted (node: JsonNode) : Result<Fact, string> =
        match tryStr "taskId" node, tryStr "targetRef" node with
        | Ok tId, Ok tRef -> Ok(Squad(FastForwardCompleted {| TaskId = tId; TargetRef = tRef |}))
        | res1, res2 ->
            let err =
                match res1, res2 with
                | Error e, _
                | _, Error e -> e
                | _ -> "Invalid FastForwardCompleted node"

            Error err

    let private decodeSquadFact (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match tag with
        | "TaskVerifiedFact" -> Some(decodeTaskVerifiedFact node)
        | "WaveAccepted" -> Some(decodeWaveAccepted node)
        | "FastForwardCompleted" -> Some(decodeFastForwardCompleted node)
        | _ -> None

    let decodeFactPart3 (tag: string) (node: JsonNode) : Result<Fact, string> option =
        match decodeChildProcessFact tag node with
        | Some res -> Some res
        | None -> decodeSquadFact tag node
