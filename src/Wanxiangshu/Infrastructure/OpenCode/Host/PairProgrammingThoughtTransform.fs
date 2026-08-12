namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Resources

/// HOST-013：永久 pair-programming auto-injected pairs。
///
/// Synthetic pair 是跨越真实 tool batch 的 temporal bracket：
/// `real calls → synthetic call → real results → synthetic result`。
/// 每个 half 的 transcript 位置只由它自己 durable 的 gap anchor 决定；同一 placement
/// occasion（SessionId + CallGap + ResultGap）最多一个 pair，重复 transform 只 replay、
/// 不再新增。同 epoch 内前次 provider wire 必须是后次 wire 的字节前缀（ARCH-004）。
module PairProgrammingThoughtTransform =

    /// HOST-013 English canonical used by tests; production loads via session language.
    let text =
        ProviderProse.render ProviderLanguage.English ProjectionConstants.PairProgrammingGuidelinePath Map.empty

    /// Provider id on a Host message (`info.providerID` or `info.model.providerID`).
    let providerIdOfMessage (rawMsg: obj) : string option =
        if isNull rawMsg then
            None
        else
            match rawMsg?info with
            | null -> None
            | info ->
                if not (isNull info?providerID) then
                    Some(unbox<string> info?providerID)
                elif not (isNull info?model) && not (isNull info?model?providerID) then
                    Some(unbox<string> info?model?providerID)
                else
                    None

    /// Most recent provider id on the transcript (assistant `providerID` or user `model.providerID`).
    let providerIdFromMessages (rawMessages: obj list) : string option =
        rawMessages |> List.rev |> List.tryPick providerIdOfMessage

    /// Emergency fuse only. Cursor is a provider-specific projection, not an
    /// occurrence bypass: it still creates/replays the same durable HOST-013 fact.
    let skipAutoInjectedRequested (_providerId: string option) : bool =
        match Environment.GetEnvironmentVariable "WANXIANGSHU_SKIP_AUTO_INJECTED" with
        | "1" -> true
        | _ -> false

    let private isCursorProvider (providerId: string option) =
        providerId
        |> Option.exists (fun value -> value.Equals("cursor", StringComparison.OrdinalIgnoreCase))

    /// The marker's source identity (HOST-013). Filtering must use this, never
    /// the text: a real user may quote the sentence.
    let source = "pair-programming-auto-injected"

    let private legacySource = "pair-programming-thought"

    let private idPrefix = "pair-programming-auto-injected-"

    /// Durable/memory pair with both halves' transcript gap anchors.
    type PairProgrammingGuidelineWire =
        { Ordinal: int64
          CallId: string
          MarkerText: string
          CallGap: TranscriptGap
          ResultGap: TranscriptGap }

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

    /// Stable identity for a Cursor text projection. The role participates in
    /// the id so the controlled three-encoder experiment cannot alias messages.
    let stableCursorMessageId (callId: string) (role: string) : string =
        let normalizedRole = role.Trim().ToLowerInvariant()
        let digest = HostDigest.sha256Hex (callId + "\n" + normalizedRole)
        "pair-programming-hint-" + digest.Substring(0, 24)

    let private buildCursorMessage (callId: string) (markerText: string) (role: string) : obj =
        let normalizedRole = role.Trim().ToLowerInvariant()

        if
            normalizedRole <> "assistant"
            && normalizedRole <> "user"
            && normalizedRole <> "system"
        then
            invalidArg "role" "Cursor Pair Hint role must be assistant, user, or system"

        createObj
            [ "info",
              box (
                  createObj
                      [ "id", box (stableCursorMessageId callId normalizedRole)
                        "role", box normalizedRole
                        "source", box source
                        "pairCallID", box callId
                        "synthetic", box true ]
              )
              "parts", box [| createObj [ "type", box "text"; "text", box markerText ] |] ]

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

                kind = "tool"
                || kind = "tool-call"
                || kind = "tool_call"
                || kind = "tool-result"
                || kind = "tool_result"

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

    // ── transcript addressing ────────────────────────────────────────────────

    /// The callId a stripped synthetic message belongs to: the call half's id
    /// is `callId + "-call"`, the result half's id is `callId`.
    let private syntheticCallIdOf (rawMsg: obj) : string option =
        if isNull rawMsg then
            None
        else
            match rawMsg?info with
            | null -> None
            | info ->
                match info?pairCallID with
                | pairCallID when not (isNull pairCallID) -> Some(unbox<string> pairCallID)
                | _ ->
                    match info?id with
                    | null -> None
                    | id ->
                        let raw = unbox<string> id

                        if raw.EndsWith("-call", StringComparison.Ordinal) then
                            Some(raw.Substring(0, raw.Length - 5))
                        else
                            Some raw

    /// Real messages in transcript order, each carrying its Host message
    /// address (`info.id` / `id`). Every real message must have one — a message
    /// without an address could never anchor a synthetic half, and a duplicate
    /// address would make anchors ambiguous.
    let private addressedRealMessages (realMessages: obj list) : Result<(string * obj) list, string> =
        let rec loop acc seen =
            function
            | [] -> Ok(List.rev acc)
            | message :: rest ->
                match ProviderWireDecode.hostMessageId message with
                | None -> Error "transcript message without address (HOST-013)"
                | Some id when Set.contains id seen -> Error(sprintf "duplicate transcript address %s (HOST-013)" id)
                | Some id -> loop ((id, message) :: acc) (Set.add id seen) rest

        loop [] Set.empty realMessages

    // ── replay：唯一合法渲染路径 ─────────────────────────────────────────────

    /// The one and only HOST-013 renderer: real messages + durable anchored
    /// pairs → the exact provider wire. Historical halves sit at their own
    /// durable gaps; nothing re-decides their position (`historyBlock` 禁止).
    ///
    /// 组内排序唯一合法：`Ordinal` 升序，同 ordinal 时 call 先于 result。
    ///
    /// Anchor 不在当前真实消息里的 historical pair **不重放、不报错**。
    /// XWire prefix probe 用 FrozenRecordPrefix 替换已覆盖前缀时会 drop 那些
    /// 消息（CTX-010 `DropLeading`）；被覆盖区里的 pair 属于被替换的前缀，
    /// 不应再注入 rewritten view，更不能因此 AbortSession 杀死 recovery slot。
    /// Durable fact 仍保留；完整 transcript 回来时 anchor 在场即可再 replay。
    type private OccurrenceProjection =
        | PairCall
        | PairResult
        | CursorText of string

    let private replay
        (providerId: string option)
        (cursorRole: string)
        (realMessages: obj list)
        (pairs: PairProgrammingGuidelineWire list)
        : Result<obj list, string> =
        match addressedRealMessages realMessages with
        | Error error -> Error error
        | Ok addressed ->
            let addressSet = addressed |> List.map fst |> Set.ofList

            let gapPresent (gap: TranscriptGap) =
                match gap with
                | TranscriptGap.Start -> true
                | TranscriptGap.Before address
                | TranscriptGap.After address -> Set.contains (TranscriptMessageAddress.value address) addressSet

            // Both durable anchors must remain present even though Cursor renders
            // only at ResultGap. CallGap is intentionally retained for reversible
            // Cursor → ordinary replay of the same occurrence.
            let placeable =
                pairs
                |> List.filter (fun pair -> gapPresent pair.CallGap && gapPresent pair.ResultGap)

            let starts = ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>()

            let before =
                Dictionary<string, ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>>()

            let after =
                Dictionary<string, ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>>()

            let addAtGap pair projection gap =
                let bucket
                    (table: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>>)
                    (address: TranscriptMessageAddress)
                    =
                    let key = TranscriptMessageAddress.value address

                    match table.TryGetValue key with
                    | true, entries -> entries.Add(pair, projection)
                    | false, _ ->
                        let entries = ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>()
                        entries.Add(pair, projection)
                        table.[key] <- entries

                match gap with
                | TranscriptGap.Start -> starts.Add(pair, projection)
                | TranscriptGap.Before address -> bucket before address
                | TranscriptGap.After address -> bucket after address

            for pair in placeable do
                if isCursorProvider providerId then
                    addAtGap pair (CursorText cursorRole) pair.ResultGap
                else
                    addAtGap pair PairCall pair.CallGap
                    addAtGap pair PairResult pair.ResultGap

            let projectionOrder =
                function
                | PairCall -> 0
                | PairResult -> 1
                | CursorText _ -> 1

            let ordered (entries: ResizeArray<PairProgrammingGuidelineWire * OccurrenceProjection>) =
                entries
                |> Seq.sortBy (fun (pair, projection) -> pair.Ordinal, projectionOrder projection)
                |> Seq.toList

            let render pair projection =
                match projection with
                | PairCall -> buildPairMessage pair.CallId pair.MarkerText true
                | PairResult -> buildPairMessage pair.CallId pair.MarkerText false
                | CursorText role -> buildCursorMessage pair.CallId pair.MarkerText role

            let output = ResizeArray<obj>()

            for pair, projection in ordered starts do
                output.Add(render pair projection)

            for address, message in addressed do
                match before.TryGetValue address with
                | true, entries ->
                    for pair, projection in ordered entries do
                        output.Add(render pair projection)
                | _ -> ()

                output.Add message

                match after.TryGetValue address with
                | true, entries ->
                    for pair, projection in ordered entries do
                        output.Add(render pair projection)
                | _ -> ()

            Ok(Seq.toList output)

    // ── 本轮新 pair 的 placement（只读当前真实消息）──────────────────────────

    /// 末端结构 → gap：
    ///
    /// - 末端存在同轮 tool batch（`Req1 Req2 Resp1 Resp2 [User]`，或 batch 直接
    ///   结尾）：`After(last call)` / `After(last result)` —— HOST-013 核心 bracket。
    /// - 无 batch 且最后一条消息是 user（trailing user）：`Before(user)` / `Before(user)`。
    /// - 无 batch、无 trailing user：`After(last real)` / `After(last real)`。
    /// - 空 transcript：`Start` / `Start`。
    ///
    /// 新 pair 的 gap 必须落在本次追加区（末尾），否则会改写已发送 wire 的中间字节、
    /// 破坏 append-only prefix。旧实现「pair 总在最后一条 user（任意位置）前」在
    /// continuation transcript（末尾是 assistant 文本）上会在中途插入新 pair —— 正是
    /// 本 Change 修复的 prefix 破坏。
    let private decideCurrentPlacement (realMessages: obj list) : Result<TranscriptGap * TranscriptGap, string> =
        match List.rev realMessages with
        | [] -> Ok(TranscriptGap.Start, TranscriptGap.Start)
        | last :: rest ->
            let lastIsUser = messageRole last = "user"
            let scanFrom = if lastIsUser then rest else (last :: rest)

            let rec takeResults found xs =
                match xs with
                | message :: tail when isToolResultMessage message -> takeResults (message :: found) tail
                | _ -> found, xs

            let rec takeCalls found xs =
                match xs with
                | message :: tail when isToolCallMessage message -> takeCalls (message :: found) tail
                | _ -> found, xs

            let resultRun, afterResults = takeResults [] scanFrom
            let callRun, _ = takeCalls [] afterResults

            match List.rev resultRun, List.rev callRun with
            | lastResult :: _, lastCall :: _ ->
                match ProviderWireDecode.hostMessageId lastCall, ProviderWireDecode.hostMessageId lastResult with
                | Some callId, Some resultId ->
                    Ok(
                        TranscriptGap.After(TranscriptMessageAddress.create callId),
                        TranscriptGap.After(TranscriptMessageAddress.create resultId)
                    )
                | _ -> Error "tool batch message without transcript address (HOST-013)"
            | _ when lastIsUser ->
                match ProviderWireDecode.hostMessageId last with
                | Some id ->
                    let address = TranscriptMessageAddress.create id
                    Ok(TranscriptGap.Before address, TranscriptGap.Before address)
                | None -> Error "trailing user without transcript address (HOST-013)"
            | _ ->
                match ProviderWireDecode.hostMessageId last with
                | Some id ->
                    let address = TranscriptMessageAddress.create id
                    Ok(TranscriptGap.After address, TranscriptGap.After address)
                | None -> Error "last message without transcript address (HOST-013)"

    // ── durable / memory history ─────────────────────────────────────────────

    let private readDurableHistory (journal: AgentJournal) (sessionId: SessionId) : PairProgrammingGuidelineWire list =
        match AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections with
        | None -> []
        | Some session ->
            session.Guidelines
            |> Option.map GuidelineProjection.pairs
            |> Option.defaultValue []
            |> List.map (fun pair ->
                { Ordinal = pair.Ordinal
                  CallId = ToolCallId.value pair.CallId
                  MarkerText = pair.MarkerText
                  CallGap = pair.CallGap
                  ResultGap = pair.ResultGap })

    let private readMemoryHistory (key: string) : PairProgrammingGuidelineWire list =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs |> Seq.toList
        | false, _ -> []

    let private appendMemory (key: string) (pair: PairProgrammingGuidelineWire) : Result<unit, string> =
        match memoryLedger.TryGetValue key with
        | true, pairs -> pairs.Add pair
        | false, _ ->
            let pairs = ResizeArray<PairProgrammingGuidelineWire>()
            pairs.Add pair
            memoryLedger.[key] <- pairs

        Ok()

    let private appendDurable
        (journal: AgentJournal)
        (sessionId: SessionId)
        (pair: PairProgrammingGuidelineWire)
        : Result<unit, string> =
        let fact =
            HostFact.PairProgrammingGuidelineAnchored
                {| SessionId = sessionId
                   Ordinal = pair.Ordinal
                   CallId = ToolCallId.create pair.CallId
                   MarkerText = pair.MarkerText
                   CallGap = pair.CallGap
                   ResultGap = pair.ResultGap |}

        match AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(JournalAppendFailure.describe failure)

    // ── 入口 ─────────────────────────────────────────────────────────────────

    /// HOST-013 commit 顺序（fail closed）：
    ///
    /// 1. 读 durable history
    /// 2. 内存 strip（只有 durable 记录能解释的 synthetic 才允许删除）
    /// 3. 决定本轮候选 placement（仅当该 placement 尚不存在）
    /// 4. 内存构造候选 fact
    /// 5. 内存渲染完整 wire（replay，校验全部 anchor）
    /// 6. append durable fact —— 失败 fail closed，禁止忽略后照发
    /// 7. 返回已校验的渲染消息
    ///
    /// 同一 placement 重复进入只 replay，不 append 新 fact。
    let private tryInjectCore
        (cursorRole: string)
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Result<obj list, string> =
        let key = transcriptKey sessionId

        let strippedCallIds =
            rawMessages
            |> List.filter isPairProgrammingThought
            |> List.choose syntheticCallIdOf

        let realMessages = rawMessages |> List.filter (isPairProgrammingThought >> not)
        let providerId = providerIdFromMessages realMessages

        if isCursorProvider providerId then
            let normalizedRole = cursorRole.Trim().ToLowerInvariant()

            if
                normalizedRole <> "assistant"
                && normalizedRole <> "user"
                && normalizedRole <> "system"
            then
                invalidArg "cursorRole" "Cursor Pair Hint role must be assistant, user, or system"

        let history, append =
            match journal, sessionId with
            | Some durable, Some sid when not (String.IsNullOrWhiteSpace sid) ->
                let session = SessionId.create sid
                readDurableHistory durable session, appendDurable durable session
            | _ -> readMemoryHistory key, appendMemory key

        // Strip is legal only when durable identity explains the synthetic. Cursor
        // text carries pairCallID because its stable wire id is role-specific.
        let knownCallIds = history |> List.map (fun pair -> pair.CallId) |> Set.ofList

        let orphaned =
            strippedCallIds
            |> List.filter (fun callId -> not (Set.contains callId knownCallIds))

        if not (List.isEmpty orphaned) then
            Error(
                sprintf
                    "synthetic messages without durable record (callId %s, HOST-013)"
                    (String.Join(", ", orphaned |> List.truncate 3))
            )
        else
            match decideCurrentPlacement realMessages with
            | Error error -> Error error
            | Ok(callGap, resultGap) ->
                let existing =
                    history
                    |> List.tryFind (fun pair -> pair.CallGap = callGap && pair.ResultGap = resultGap)

                match existing with
                | Some _ -> replay providerId cursorRole realMessages history
                | None when skipAutoInjectedRequested providerId -> replay providerId cursorRole realMessages history
                | None ->
                    let ordinal =
                        match history with
                        | [] -> 1L
                        | pairs -> (List.last pairs).Ordinal + 1L

                    let candidate =
                        { Ordinal = ordinal
                          CallId = stableCallId sessionId ordinal
                          MarkerText = markerText
                          CallGap = callGap
                          ResultGap = resultGap }

                    match replay providerId cursorRole realMessages (history @ [ candidate ]) with
                    | Error error -> Error error
                    | Ok rendered ->
                        match append candidate with
                        | Ok() -> Ok rendered
                        | Error error -> Error error

    /// Controlled C-PH encoder seam. Production uses `tryInject`, which fixes
    /// Cursor to assistant role; tests may compare user/system with identical bytes.
    let tryInjectWithCursorRole
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        (cursorRole: string)
        : Result<obj list, string> =
        tryInjectCore cursorRole journal sessionId markerText rawMessages

    let tryInject
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Result<obj list, string> =
        tryInjectCore "assistant" journal sessionId markerText rawMessages
