namespace Wanxiangshu.Next.Journal

open System
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal.FactSubEncoders
open Wanxiangshu.Next.Journal.FactDecoders

module FactCodec =

    let private obj (pairs: (string * JsonNode) list) : JsonObject =
        let node = JsonObject()

        for (k, v) in pairs do
            node.Add(k, v)

        node

    let private str (v: string) : JsonNode = JsonValue.Create(v) :> JsonNode
    let private intNode (v: int) : JsonNode = JsonValue.Create(v) :> JsonNode

    let private strArray (items: string list) : JsonArray =
        let arr = JsonArray()

        for item in items do
            arr.Add(JsonValue.Create(item))

        arr

    let private encodeRuntimeFact (rFact: RuntimeFact) : JsonObject =
        match rFact with
        | RuntimeStarted r ->
            obj
                [ "tag", str "RuntimeStarted"
                  "runtimeId", str (RuntimeId.value r.RuntimeId)
                  "processId", intNode r.ProcessId
                  "startedAt", str (r.StartedAt.ToString("o")) ]

    let private encodeSessionFact (sFact: SessionFact) : JsonObject =
        match sFact with
        | HumanTurnStarted r -> obj [ "tag", str "HumanTurnStarted"; "turnId", str (TurnId.value r.TurnId) ]
        | SessionSettled r -> obj [ "tag", str "SessionSettled"; "result", encodeSessionResult r.Result ]

    let private encodePromptFact (pFact: PromptFact) : JsonObject =
        match pFact with
        | PromptRequested r ->
            obj
                [ "tag", str "PromptRequested"
                  "promptKey", str r.PromptKey
                  "turnId", str (TurnId.value r.TurnId)
                  "purpose", str r.Purpose ]
        | PromptSubmitted r ->
            obj
                [ "tag", str "PromptSubmitted"
                  "promptKey", str r.PromptKey
                  "messageId", str (MessageId.value r.MessageId) ]
        | PromptTerminal r ->
            let pairs =
                [ "tag", str "PromptTerminal"
                  "promptKey", str r.PromptKey
                  "outcome", encodePromptOutcome r.Outcome :> JsonNode ]

            match r.AssistantMessageId with
            | Some msgId -> obj (pairs @ [ "assistantMessageId", str (MessageId.value msgId) ])
            | None -> obj pairs

    let private encodeReviewFact (rFact: ReviewFact) : JsonObject =
        match rFact with
        | ReviewApplied r ->
            let fields =
                [ "tag", str "ReviewApplied"
                  "verdict", encodeReviewVerdict r.Verdict :> JsonNode
                  "round", intNode r.Round ]

            match r.ResultingTodo with
            | Some todo -> obj (fields @ [ "resultingTodo", encodeTodoSnapshot todo :> JsonNode ])
            | None -> obj fields

    let private encodeChildFact (cFact: ChildFact) : JsonObject =
        match cFact with
        | ChildCreated r ->
            obj
                [ "tag", str "ChildCreated"
                  "childId", str (ChildId.value r.ChildId)
                  "targetAgent", str r.TargetAgent ]
        | ChildCompletedFact r ->
            obj
                [ "tag", str "ChildCompletedFact"
                  "childId", str (ChildId.value r.ChildId)
                  "result", encodeChildResult r.Result ]

    let private encodeProcessFact (prFact: ProcessFact) : JsonObject =
        match prFact with
        | ProcessSpawned r ->
            obj
                [ "tag", str "ProcessSpawned"
                  "processId", str (ProcessId.value r.ProcessId)
                  "command", str r.Command ]
        | ProcessExited r ->
            obj
                [ "tag", str "ProcessExited"
                  "processId", str (ProcessId.value r.ProcessId)
                  "result", encodeProcessResult r.Result ]

    let private encodeSquadFact (sqFact: SquadFact) : JsonObject =
        match sqFact with
        | TaskVerifiedFact r ->
            obj
                [ "tag", str "TaskVerifiedFact"
                  "taskId", str r.TaskId
                  "result", encodeSquadTaskResult r.Result ]
        | WaveAccepted r ->
            obj
                [ "tag", str "WaveAccepted"
                  "waveIndex", intNode r.WaveIndex
                  "acceptedTaskIds", strArray r.AcceptedTaskIds ]
        | FastForwardCompleted r ->
            obj
                [ "tag", str "FastForwardCompleted"
                  "taskId", str r.TaskId
                  "targetRef", str r.TargetRef ]

    let upgradeFactNode (node: JsonNode) : JsonNode =
        let clone = node.DeepClone()

        if clone.["version"] <> null && clone.["version"].GetValue<int>() >= 1 then
            clone
        else
            clone.["version"] <- JsonValue.Create(1) :> JsonNode
            clone

    let encodeFact (fact: Fact) : JsonObject =
        let node =
            match fact with
            | Runtime f -> encodeRuntimeFact f
            | Session f -> encodeSessionFact f
            | Todo(TodoChanged r) -> obj [ "tag", str "TodoChanged"; "snapshot", encodeTodoSnapshot r.Snapshot ]
            | Prompt f -> encodePromptFact f
            | Review f -> encodeReviewFact f
            | Child f -> encodeChildFact f
            | Process f -> encodeProcessFact f
            | Squad f -> encodeSquadFact f

        node.["version"] <- JsonValue.Create(1) :> JsonNode
        node

    let decodeFact (node: JsonNode) : Result<Fact, string> =
        try
            let version =
                if node.["version"] <> null then
                    node.["version"].GetValue<int>()
                else
                    0

            let upgradedNode = upgradeFactNode node
            let tag = upgradedNode.["tag"].GetValue<string>()

            match decodeFactPart1 tag upgradedNode with
            | Some res -> res
            | None ->
                match decodeFactPart2 tag upgradedNode with
                | Some res -> res
                | None ->
                    match decodeFactPart3 tag upgradedNode with
                    | Some res -> res
                    | None -> Error $"Unknown Fact tag: {tag}"
        with ex ->
            Error $"Failed decoding Fact: {ex.Message}"

    let serializeFact (fact: Fact) : string =
        let node = encodeFact fact
        node.ToJsonString()

    let deserializeFact (json: string) : Result<Fact, string> =
        try
            let node = JsonNode.Parse(json)

            if node = null then
                Error "Null Fact JSON"
            else
                decodeFact node
        with ex ->
            Error $"Failed parsing Fact JSON: {ex.Message}"
