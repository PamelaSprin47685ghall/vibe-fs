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

/// HOST-013：永久 pair-programming guideline pairs。
///
/// 每次 transform 读取完整 durable pair 序列，按原字节恢复，再在全局末尾追加
/// 本次 tool-call + tool-result。无 anchor 门槛；不删除历史 pair。
module PairProgrammingThoughtTransform =

    /// HOST-013 guideline 正文。Domain 单源。
    let text = ProjectionConstants.PairProgrammingGuidelineText

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-guideline"

    let private legacySource = "pair-programming-thought"

    let private idPrefix = "pair-programming-guideline-"

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

    let private buildPairMessage (rolePart: string) (callId: string) (markerText: string) (isCall: bool) : obj =
        let part =
            if isCall then
                createObj
                    [ "type", box "tool"
                      "tool", box "guideline"
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
                      "tool", box "guideline"
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
        [ buildPairMessage "assistant" callId markerText true
          buildPairMessage "assistant" callId markerText false ]

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

    /// HOST-013：无门槛恢复历史 pair，并在末尾追加本次 pair。
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
            let intent =
                ProjectionIntent.InsertPairProgrammingThought { History = history; Next = next }

            match ProjectionPlanner.plan [ intent ] with
            | Error _ -> None
            | Ok ordered ->
                let baseWire = retainedRaw |> List.choose Projection.decodeMessage

                let wire =
                    ProjectionRenderer.renderMessagesWithIntents emptySnapshot baseWire ordered

                let expectedPairs =
                    (history @ [ next ])
                    |> List.collect (fun pair ->
                        let callId = ToolCallId.create pair.CallId

                        let callMsg: WireMessage =
                            { Role = "assistant"
                              Parts = [ WireToolCall(callId, "guideline", "{}") ] }

                        let resultMsg: WireMessage =
                            { Role = "assistant"
                              Parts = [ WireToolResult(callId, pair.MarkerText) ] }

                        [ callMsg; resultMsg ])

                let expected = baseWire @ expectedPairs

                if wire <> expected then
                    None
                else
                    let pairObjs =
                        (history @ [ next ])
                        |> List.collect (fun pair -> buildPair pair.CallId pair.MarkerText)

                    Some(retainedRaw @ pairObjs)
