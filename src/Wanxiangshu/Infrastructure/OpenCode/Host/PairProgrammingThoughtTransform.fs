namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// HOST-013：永久 pair-programming auto-injected pairs。
///
/// 每次 transform 读取完整 durable pair 序列，按原字节恢复 history，
/// 再把本次 pair 插在 trailing user 之前（多 tool 时 call/result 批末）。
module PairProgrammingThoughtTransform =

    /// HOST-013 auto-injected 正文。Domain 单源。
    let text = ProjectionConstants.PairProgrammingGuidelineText

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-auto-injected"

    let private legacySource = "pair-programming-thought"

    let private idPrefix = "pair-programming-auto-injected-"

    /// Process-local fallback when journal is unavailable (tests / no workspace).
    /// Keyed by transcript identity; append-only within process.
    let private memoryLedger =
        Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>()

    /// HOST-013：marker 身份仅按 `info.source`。
    let isPairProgrammingThought (rawMsg: obj) : bool =
        if isNull rawMsg then
            false
        else
            match rawMsg?info with
            | null -> false
            | info ->
                let markerSource = unbox<string> info?source
                markerSource = source || markerSource = legacySource

    let private transcriptKey (sessionId: string option) : string = defaultArg sessionId ""

    /// CallId = digest(transcript + source + ordinal). Stable across restarts.
    let stableCallId (sessionId: string option) (ordinal: int64) : string =
        let digest =
            HostDigest.sha256Hex ((transcriptKey sessionId) + source + string ordinal)

        idPrefix + digest.Substring(0, 24)

    let private buildPairMessage (callId: string) (markerText: string) (isCall: bool) : obj =
        let part =
            if isCall then
                createObj
                    [ "type", box "tool"
                      "tool", box "auto-injected"
                      "callID", box callId
                      "state",
                      box (
                          createObj
                              [ "status", box "pending"
                                "input", box (createObj [])
                                "time", box (createObj [ "start", box 0 ]) ]
                      ) ]
            else
                createObj
                    [ "type", box "tool"
                      "tool", box "auto-injected"
                      "callID", box callId
                      "state",
                      box (
                          createObj
                              [ "status", box "completed"
                                "input", box (createObj [])
                                "output", box markerText
                                "time", box (createObj [ "start", box 0; "end", box 0 ]) ]
                      ) ]

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box (if isCall then callId + "-call" else callId)
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts", box [| part |] ]

    let private buildPair (callId: string) (markerText: string) : obj list =
        [ buildPairMessage callId markerText true
          buildPairMessage callId markerText false ]

    let private messageRole (rawMsg: obj) : string =
        if isNull rawMsg then
            ""
        else
            let fromInfo =
                match rawMsg?info with
                | null -> None
                | info ->
                    match info?role with
                    | null -> None
                    | role -> Some(unbox<string> role)

            let fromTop =
                match rawMsg?role with
                | null -> None
                | role -> Some(unbox<string> role)

            defaultArg fromInfo (defaultArg fromTop "")
            |> fun value -> value.ToLowerInvariant()

    let private isJsArray (value: obj) : bool =
        not (isNull value) && emitJsExpr value "Array.isArray($0)"

    let private rawParts (rawMsg: obj) : obj array =
        if isNull rawMsg then
            [||]
        else
            match rawMsg?parts with
            | null ->
                match rawMsg?content with
                | null -> [||]
                | content when isJsArray content -> unbox<obj array> content
                | _ -> [||]
            | parts when isJsArray parts -> unbox<obj array> parts
            | _ -> [||]

    let private isToolPart (part: obj) : bool =
        if isNull part then
            false
        else
            match part?``type`` with
            | null -> false
            | t ->
                let kind = (unbox<string> t).ToLowerInvariant()
                kind = "tool" || kind = "tool-call" || kind = "tool_call" || kind = "tool-result" || kind = "tool_result"

    let private partStatus (part: obj) : string option =
        if isNull part then
            None
        else
            match part?state with
            | null -> None
            | state ->
                match state?status with
                | null -> None
                | status -> Some((unbox<string> status).ToLowerInvariant())

    /// Host raw：pending/running tool part = call；completed/error = result。
    let private isToolCallMessage (rawMsg: obj) : bool =
        rawParts rawMsg
        |> Array.exists (fun part ->
            isToolPart part
            && match partStatus part with
               | Some "completed"
               | Some "error" -> false
               | _ -> true)

    let private isToolResultMessage (rawMsg: obj) : bool =
        rawParts rawMsg
        |> Array.exists (fun part ->
            isToolPart part
            && match partStatus part with
               | Some "completed"
               | Some "error" -> true
               | _ -> false)

    /// 将 history + next 插入 retained：trailing user 前；多 tool 时 call/result 批末。
    let private placePairs (retained: obj list) (pairs: PairProgrammingGuidelineWire list) : obj list =
        match pairs with
        | [] -> retained
        | _ ->
            let arr = retained |> List.toArray
            let trailingUserIdx =
                let mutable idx = arr.Length - 1
                let mutable found = -1

                while idx >= 0 && found < 0 do
                    if messageRole arr.[idx] = "user" then
                        found <- idx

                    idx <- idx - 1

                found

            let headLen = if trailingUserIdx < 0 then arr.Length else trailingUserIdx
            let head = if headLen = 0 then [||] else arr.[0 .. headLen - 1]
            let tail = if trailingUserIdx < 0 then [||] else arr.[trailingUserIdx ..]

            let mutable resultStart = head.Length

            while resultStart > 0 && isToolResultMessage head.[resultStart - 1] do
                resultStart <- resultStart - 1

            let mutable callStart = resultStart

            while callStart > 0 && isToolCallMessage head.[callStart - 1] do
                callStart <- callStart - 1

            let historyPairs, nextPair =
                match List.rev pairs with
                | next :: rest -> List.rev rest, next
                | [] -> [], Unchecked.defaultof<_>

            let historyBlock =
                historyPairs
                |> List.collect (fun pair -> buildPair pair.CallId pair.MarkerText)

            let nextCall = buildPairMessage nextPair.CallId nextPair.MarkerText true
            let nextResult = buildPairMessage nextPair.CallId nextPair.MarkerText false

            let prefix = if callStart = 0 then [] else head.[0 .. callStart - 1] |> Array.toList
            let calls = if callStart >= resultStart then [] else head.[callStart .. resultStart - 1] |> Array.toList
            let results = if resultStart >= head.Length then [] else head.[resultStart ..] |> Array.toList
            let tailList = tail |> Array.toList

            if callStart < head.Length then
                // 有同轮 tool 批（call 和/或 result）：history 在批前；next call/result 批末。
                prefix @ historyBlock @ calls @ [ nextCall ] @ results @ [ nextResult ] @ tailList
            else
                // 无 tool 批：history + next 相邻，整体在 trailing user 前。
                (head |> Array.toList) @ historyBlock @ [ nextCall; nextResult ] @ tailList

    let private readDurableHistory (journal: AgentJournal) (sessionId: SessionId) : PairProgrammingGuidelineWire list =
        match AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections with
        | None -> []
        | Some session ->
            session.Guidelines
            |> Option.map GuidelineProjection.pairs
            |> Option.defaultValue []
            |> List.map (fun pair ->
                { CallId = ToolCallId.value pair.CallId
                  MarkerText = pair.MarkerText })

    let private readMemoryHistory (key: string) : PairProgrammingGuidelineWire list =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs |> Seq.toList
        | false, _ -> []

    let private appendMemory (key: string) (pair: PairProgrammingGuidelineWire) : unit =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs.Add pair
        | false, _ ->
            let pairs = ResizeArray<PairProgrammingGuidelineWire>()
            pairs.Add pair
            memoryLedger.[key] <- pairs

    let private appendDurable
        (journal: AgentJournal)
        (sessionId: SessionId)
        (ordinal: int64)
        (callId: string)
        (markerText: string)
        : Result<unit, string> =
        let fact =
            HostFact.PairProgrammingGuidelineAppended
                {| SessionId = sessionId
                   Ordinal = ordinal
                   CallId = ToolCallId.create callId
                   MarkerText = markerText |}

        match AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(JournalAppendFailure.describe failure)

    let private emptySnapshot: ProjectionSnapshot =
        { CurrentProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = [] }
          CommittedPrefix = None
          BlogFrames = []
          TransportMessages = Set.empty
          HostReanchor = None }

    let private wireIsToolCall (message: WireMessage) : bool =
        match message.Parts with
        | ProviderProjection.WireToolCall _ :: _ -> true
        | _ -> false

    let private wireIsToolResult (message: WireMessage) : bool =
        match message.Parts with
        | ProviderProjection.WireToolResult _ :: _ -> true
        | _ -> false

    let private wirePairMessages (pair: PairProgrammingGuidelineWire) : WireMessage list =
        let callId = ToolCallId.create pair.CallId

        [ { Role = "assistant"
            Parts = [ WireToolCall(callId, "auto-injected", "{}") ] }
          { Role = "assistant"
            Parts = [ WireToolResult(callId, pair.MarkerText) ] } ]

    /// Canonical wire 放置：与 raw placePairs 同构。
    let private placeWirePairs (retained: WireMessage list) (pairs: PairProgrammingGuidelineWire list) : WireMessage list =
        match pairs with
        | [] -> retained
        | _ ->
            let arr = retained |> List.toArray
            let trailingUserIdx =
                let mutable idx = arr.Length - 1
                let mutable found = -1

                while idx >= 0 && found < 0 do
                    if arr.[idx].Role = "user" then
                        found <- idx

                    idx <- idx - 1

                found

            let headLen = if trailingUserIdx < 0 then arr.Length else trailingUserIdx
            let head = if headLen = 0 then [||] else arr.[0 .. headLen - 1]
            let tail = if trailingUserIdx < 0 then [||] else arr.[trailingUserIdx ..]

            let mutable resultStart = head.Length

            while resultStart > 0 && wireIsToolResult head.[resultStart - 1] do
                resultStart <- resultStart - 1

            let mutable callStart = resultStart

            while callStart > 0 && wireIsToolCall head.[callStart - 1] do
                callStart <- callStart - 1

            let historyPairs, nextPair =
                match List.rev pairs with
                | next :: rest -> List.rev rest, next
                | [] -> [], Unchecked.defaultof<_>

            let historyBlock = historyPairs |> List.collect wirePairMessages
            let nextMsgs = wirePairMessages nextPair
            let nextCall = List.head nextMsgs
            let nextResult = List.item 1 nextMsgs

            let prefix = if callStart = 0 then [] else head.[0 .. callStart - 1] |> Array.toList
            let calls = if callStart >= resultStart then [] else head.[callStart .. resultStart - 1] |> Array.toList
            let results = if resultStart >= head.Length then [] else head.[resultStart ..] |> Array.toList
            let tailList = tail |> Array.toList

            if callStart < head.Length then
                prefix @ historyBlock @ calls @ [ nextCall ] @ results @ [ nextResult ] @ tailList
            else
                (head |> Array.toList) @ historyBlock @ nextMsgs @ tailList

    /// HOST-013：恢复 history，本次 pair 插在 trailing user 前。
    ///
    /// - journal + sessionId：durable append-only 事实
    /// - 否则：process-local memory ledger（同键永久追加）
    /// 始终返回 Some（空历史同样有效）。
    let tryInject
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : obj list option =
        let key = transcriptKey sessionId
        let retainedRaw = rawMessages |> List.filter (isPairProgrammingThought >> not)

        let history, appendNext =
            match journal, sessionId with
            | Some durable, Some sid when not (System.String.IsNullOrWhiteSpace sid) ->
                let session = SessionId.create sid
                let history = readDurableHistory durable session
                let ordinal = int64 history.Length + 1L
                let callId = stableCallId sessionId ordinal

                let append () =
                    match appendDurable durable session ordinal callId markerText with
                    | Ok() ->
                        Some
                            { CallId = callId
                              MarkerText = markerText }
                    | Error _ -> None

                history, append
            | _ ->
                let history = readMemoryHistory key
                let ordinal = int64 history.Length + 1L
                let callId = stableCallId sessionId ordinal

                let append () =
                    let next =
                        { CallId = callId
                          MarkerText = markerText }

                    appendMemory key next
                    Some next

                history, append

        match appendNext () with
        | None -> None
        | Some next ->
            let allPairs = history @ [ next ]

            let intent =
                ProjectionIntent.InsertPairProgrammingThought { History = history; Next = next }

            match ProjectionPlanner.plan [ intent ] with
            | Error _ -> None
            | Ok ordered ->
                let baseWire = retainedRaw |> List.choose Projection.decodeMessage

                let wire =
                    ProjectionRenderer.renderMessagesWithIntents emptySnapshot baseWire ordered

                let expected = placeWirePairs baseWire allPairs

                if wire <> expected then
                    None
                else
                    Some(placePairs retainedRaw allPairs)
