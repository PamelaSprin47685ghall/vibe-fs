namespace Wanxiangshu.Next.Journal

open System
open System.Text.Json.Nodes
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact

module FactSubEncoders =

    let private obj (pairs: (string * JsonNode) list) : JsonObject =
        let node = JsonObject()

        for (k, v) in pairs do
            node.Add(k, v)

        node

    let private str (v: string) : JsonNode = JsonValue.Create(v) :> JsonNode
    let private intNode (v: int) : JsonNode = JsonValue.Create(v) :> JsonNode
    let private boolNode (v: bool) : JsonNode = JsonValue.Create(v) :> JsonNode

    let private strArray (items: string list) : JsonArray =
        let arr = JsonArray()

        for item in items do
            arr.Add(JsonValue.Create(item))

        arr

    let private parseStrArray (node: JsonNode) : Result<string list, string> =
        if node = null then
            Error "Null array node"
        else
            match node with
            | :? JsonArray as arr ->
                let mutable parseErr = None

                let items =
                    [ for item in arr do
                          if parseErr.IsNone then
                              if item = null then
                                  parseErr <- Some "String array contained null or non-string element"
                              else
                                  try
                                      yield item.GetValue<string>()
                                  with ex ->
                                      parseErr <- Some $"Failed parsing string array item: {ex.Message}" ]

                match parseErr with
                | Some err -> Error err
                | None -> Ok items
            | _ -> Error "Expected JsonArray"

    let encodeSessionResult (res: SessionResult) : JsonObject =
        match res with
        | Completed msg -> obj [ "tag", str "Completed"; "message", str msg ]
        | Cancelled msg -> obj [ "tag", str "Cancelled"; "message", str msg ]
        | Failed msg -> obj [ "tag", str "Failed"; "message", str msg ]

    let decodeSessionResult (node: JsonNode) : Result<SessionResult, string> =
        try
            let tag = node.["tag"].GetValue<string>()

            match tag with
            | "Completed" -> Ok(Completed(node.["message"].GetValue<string>()))
            | "Cancelled" -> Ok(Cancelled(node.["message"].GetValue<string>()))
            | "Failed" -> Ok(Failed(node.["message"].GetValue<string>()))
            | other -> Error $"Unknown SessionResult tag: {other}"
        with ex ->
            Error $"Failed decoding SessionResult: {ex.Message}"

    let encodePromptOutcome (outcome: PromptOutcome) : JsonObject =
        match outcome with
        | Delivered msgId -> obj [ "tag", str "Delivered"; "messageId", str (MessageId.value msgId) ]
        | RetryableFailure reason -> obj [ "tag", str "RetryableFailure"; "reason", str reason ]
        | AcceptanceUnknown(reason, msgIdOpt) ->
            let pairs = [ "tag", str "AcceptanceUnknown"; "reason", str reason ]

            match msgIdOpt with
            | Some msgId -> obj (pairs @ [ "messageId", str (MessageId.value msgId) ])
            | None -> obj pairs
        | FatalFailure reason -> obj [ "tag", str "FatalFailure"; "reason", str reason ]

    let decodePromptOutcome (node: JsonNode) : Result<PromptOutcome, string> =
        try
            let tag = node.["tag"].GetValue<string>()

            match tag with
            | "Delivered" -> Ok(Delivered(MessageId.create (node.["messageId"].GetValue<string>())))
            | "RetryableFailure" -> Ok(RetryableFailure(node.["reason"].GetValue<string>()))
            | "AcceptanceUnknown" ->
                let reason = node.["reason"].GetValue<string>()

                let msgIdOpt =
                    if node.["messageId"] <> null then
                        Some(MessageId.create (node.["messageId"].GetValue<string>()))
                    else
                        None

                Ok(AcceptanceUnknown(reason, msgIdOpt))
            | "FatalFailure" -> Ok(FatalFailure(node.["reason"].GetValue<string>()))
            | other -> Error $"Unknown PromptOutcome tag: {other}"
        with ex ->
            Error $"Failed decoding PromptOutcome: {ex.Message}"

    let encodeReviewVerdict (verdict: ReviewVerdict) : JsonObject =
        match verdict with
        | ReviewVerdict.Passed -> obj [ "tag", str "Passed" ]
        | ReviewVerdict.NeedsChanges reqs -> obj [ "tag", str "NeedsChanges"; "changeRequests", strArray reqs ]
        | ReviewVerdict.Invalid reason -> obj [ "tag", str "Invalid"; "reason", str reason ]

    let decodeReviewVerdict (node: JsonNode) : Result<ReviewVerdict, string> =
        try
            let tag = node.["tag"].GetValue<string>()

            match tag with
            | "Passed" -> Ok ReviewVerdict.Passed
            | "NeedsChanges" ->
                match parseStrArray node.["changeRequests"] with
                | Ok reqs -> Ok(ReviewVerdict.NeedsChanges reqs)
                | Error err -> Error err
            | "Invalid" -> Ok(ReviewVerdict.Invalid(node.["reason"].GetValue<string>()))
            | other -> Error $"Unknown ReviewVerdict tag: {other}"
        with ex ->
            Error $"Failed decoding ReviewVerdict: {ex.Message}"

    let encodeChildResult (res: ChildResult) : JsonObject =
        match res with
        | ChildCompleted summary -> obj [ "tag", str "ChildCompleted"; "summary", str summary ]
        | ChildCancelled reason -> obj [ "tag", str "ChildCancelled"; "reason", str reason ]
        | ChildFailed error -> obj [ "tag", str "ChildFailed"; "error", str error ]

    let decodeChildResult (node: JsonNode) : Result<ChildResult, string> =
        try
            let tag = node.["tag"].GetValue<string>()

            match tag with
            | "ChildCompleted" -> Ok(ChildCompleted(node.["summary"].GetValue<string>()))
            | "ChildCancelled" -> Ok(ChildCancelled(node.["reason"].GetValue<string>()))
            | "ChildFailed" -> Ok(ChildFailed(node.["error"].GetValue<string>()))
            | other -> Error $"Unknown ChildResult tag: {other}"
        with ex ->
            Error $"Failed decoding ChildResult: {ex.Message}"

    let encodeProcessResult (res: ProcessResult) : JsonObject =
        obj
            [ "exitCode", intNode res.ExitCode
              "stdout", str res.Stdout
              "stderr", str res.Stderr
              "stdoutTruncated", boolNode res.StdoutTruncated
              "stderrTruncated", boolNode res.StderrTruncated ]

    let decodeProcessResult (node: JsonNode) : Result<ProcessResult, string> =
        try
            let res: ProcessResult =
                { ExitCode = node.["exitCode"].GetValue<int>()
                  Stdout = node.["stdout"].GetValue<string>()
                  Stderr = node.["stderr"].GetValue<string>()
                  StdoutTruncated = node.["stdoutTruncated"].GetValue<bool>()
                  StderrTruncated = node.["stderrTruncated"].GetValue<bool>() }

            Ok res
        with ex ->
            Error $"Failed decoding ProcessResult: {ex.Message}"

    let encodeSquadTaskResult (res: SquadTaskResult) : JsonObject =
        match res with
        | TaskVerified summary -> obj [ "tag", str "TaskVerified"; "summary", str summary ]
        | TaskFailed error -> obj [ "tag", str "TaskFailed"; "error", str error ]

    let decodeSquadTaskResult (node: JsonNode) : Result<SquadTaskResult, string> =
        try
            let tag = node.["tag"].GetValue<string>()

            match tag with
            | "TaskVerified" -> Ok(TaskVerified(node.["summary"].GetValue<string>()))
            | "TaskFailed" -> Ok(TaskFailed(node.["error"].GetValue<string>()))
            | other -> Error $"Unknown SquadTaskResult tag: {other}"
        with ex ->
            Error $"Failed decoding SquadTaskResult: {ex.Message}"

    let encodeTodoSnapshot (snap: TodoSnapshot) : JsonObject = obj [ "items", strArray snap.Items ]

    let decodeTodoSnapshot (node: JsonNode) : Result<TodoSnapshot, string> =
        try
            match parseStrArray node.["items"] with
            | Ok items -> Ok { Items = items }
            | Error err -> Error err
        with ex ->
            Error $"Failed decoding TodoSnapshot: {ex.Message}"

    let getStrArray (node: JsonNode) : Result<string list, string> = parseStrArray node
