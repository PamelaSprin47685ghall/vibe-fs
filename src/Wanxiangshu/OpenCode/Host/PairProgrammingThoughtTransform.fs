namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources

/// HOST-013：永久 pair-programming auto-injected pairs。
///
/// Ordinary Host 编码是 ResultGap 上的一条 completed `auto-injected` tool part。
/// OpenCode `toModelMessagesEffect` 把它展开成 provider tool-call + tool-result。
/// 禁止 pending/running：Host 会把它们收成 "[Tool execution was interrupted]"。
/// 每个 occurrence 的 transcript 位置由 durable CallGap/ResultGap 决定；同一 placement
/// occasion（SessionId + CallGap + ResultGap）最多一个 pair，重复 transform 只 replay、
/// 不再新增。同 epoch 内前次 provider wire 必须是后次 wire 的字节前缀（ARCH-004）。
module PairProgrammingThoughtTransform =

    /// HOST-013 English canonical used by tests; production loads via session language.
    let text =
        ProviderProse.render ProviderLanguage.English ProjectionConstants.PairProgrammingGuidelinePath Map.empty

    // ── JS Evidence parsers（flat；无嵌套 decision）──────────────────────────

    let private tryObj (value: obj) : obj option =
        if isNull value then None else Some value

    let private tryUnboxString (value: obj) : string option =
        if isNull value then None else Some(unbox<string> value)

    let private messageInfo (rawMsg: obj) : obj option =
        if isNull rawMsg then None
        elif isNull rawMsg?info then None
        else Some(rawMsg?info)

    let private providerIdFromInfo (info: obj) : string option =
        if not (isNull info?providerID) then
            Some(unbox<string> info?providerID)
        elif not (isNull info?model) && not (isNull info?model?providerID) then
            Some(unbox<string> info?model?providerID)
        else
            None

    /// Provider id on a Host message (`info.providerID` or `info.model.providerID`).
    let providerIdOfMessage (rawMsg: obj) : string option =
        messageInfo rawMsg |> Option.bind providerIdFromInfo

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

    /// Marker tool name on the wire (HOST-013). Meaningless identifier to prevent LLM abuse.
    let toolName = "-"

    let private legacyToolName = "auto-injected"

    let private isMarkerSource (markerSource: string) =
        markerSource = source || markerSource = legacySource

    /// HOST-013：marker 身份仅按 `info.source`。
    let isPairProgrammingThought (rawMsg: obj) : bool =
        messageInfo rawMsg
        |> Option.map (fun info -> unbox<string> info?source)
        |> Option.exists isMarkerSource

    /// Scolding text for active model calls to the non-executable placeholder.
    let reprimandText (lang: ProviderLanguage option) : string =
        match lang with
        | Some ProviderLanguage.SimplifiedChinese ->
            "DENIED. `-` 不是可执行工具，禁止主动调用。这是系统内部占位标记，没有任何功能。请专注于你的本职任务，不要尝试调用内部标记符号。"
        | _ ->
            "DENIED. `-` is not an executable tool. Do not call this symbol. This is an internal system marker with no functionality. Focus on your assigned tasks and do not attempt to invoke non-existent internal symbols."

    let private isMatchingToolName (name: obj) : bool =
        match tryUnboxString name with
        | Some s -> s = toolName || s = legacyToolName
        | None -> false

    let private isHyphenToolType (part: obj) : bool =
        match tryUnboxString part?``type`` with
        | None -> false
        | Some t ->
            let kind = t.ToLowerInvariant()
            kind = "tool--" || kind = "tool-auto-injected"

    let private isHyphenToolPart (part: obj) : bool =
        if isNull part then
            false
        else
            isMatchingToolName part?tool
            || isMatchingToolName part?name
            || isHyphenToolType part

    let private writeCompletedReprimandState (reprimand: string) (part: obj) (originalState: obj) =
        if isNull originalState then
            let s =
                createObj
                    [ "status", box "completed"
                      "input", box (createObj [])
                      "output", box reprimand
                      "time", box (createObj [ "start", box 0; "end", box 0 ]) ]

            part?state <- s
        else
            originalState?status <- box "completed"
            originalState?output <- box reprimand
            emitJsExpr originalState "delete $0.error; delete $0.errorText" |> ignore

    let private clearPartError (part: obj) =
        if not (isNull part?error) then
            emitJsExpr part "delete $0.error; delete $0.errorText" |> ignore

    let private overwritePartOutput (reprimand: string) (part: obj) =
        if not (isNull part?output) then
            part?output <- box reprimand

    let private applyHyphenReprimand (reprimand: string) (part: obj) : obj =
        writeCompletedReprimandState reprimand part (part?state)
        clearPartError part
        overwritePartOutput reprimand part
        part

    let private reprimandToolPart (lang: ProviderLanguage option) (part: obj) : obj =
        if not (isHyphenToolPart part) then
            part
        else
            applyHyphenReprimand (reprimandText lang) part

    let private isJsArray (value: obj) : bool =
        not (isNull value) && emitJsExpr value "Array.isArray($0)"

    let private asJsArray (value: obj) : obj array option =
        if isNull value then None
        elif isJsArray value then Some(unbox<obj array> value)
        else None

    let private rawParts (rawMsg: obj) : obj array =
        if isNull rawMsg then
            [||]
        else
            asJsArray rawMsg?parts
            |> Option.orElseWith (fun () -> asJsArray rawMsg?content)
            |> Option.defaultValue [||]

    let private reprimandHyphenParts (lang: ProviderLanguage option) (parts: obj array) =
        parts
        |> Array.filter isHyphenToolPart
        |> Array.iter (fun part -> reprimandToolPart lang part |> ignore)

    let private sanitizeNonMarkerMessage (lang: ProviderLanguage option) (rawMsg: obj) : obj =
        let parts = rawParts rawMsg

        if Array.isEmpty parts then
            rawMsg
        else
            reprimandHyphenParts lang parts
            rawMsg

    let private sanitizeActiveHyphenMessage (lang: ProviderLanguage option) (rawMsg: obj) : obj =
        if isNull rawMsg || isPairProgrammingThought rawMsg then
            rawMsg
        else
            sanitizeNonMarkerMessage lang rawMsg

    /// Transform any active LLM calls to `-` on real messages from failed into completed with reprimand.
    let sanitizeActiveToolCalls (lang: ProviderLanguage option) (rawMessages: obj list) : obj list =
        rawMessages |> List.map (sanitizeActiveHyphenMessage lang)

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

    let private transcriptKey (sessionId: string option) : string = defaultArg sessionId ""

    /// CallId = digest(transcript + source + ordinal). Stable across restarts.
    let stableCallId (sessionId: string option) (ordinal: int64) : string =
        let digest =
            HostDigest.sha256Hex ((transcriptKey sessionId) + source + string ordinal)

        idPrefix + digest.Substring(0, 24)

    let private buildPairMessage (callId: string) (markerText: string) : obj =
        let part =
            createObj
                [ "type", box "tool"
                  "tool", box toolName
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
                      [ "id", box callId
                        "role", box "assistant"
                        "source", box source
                        "synthetic", box true ]
              )
              "parts", box [| part |] ]

    let private roleFrom (target: obj) : string option = tryUnboxString target?role

    let private messageRole (rawMsg: obj) : string =
        if isNull rawMsg then
            ""
        else
            messageInfo rawMsg
            |> Option.bind roleFrom
            |> Option.orElseWith (fun () -> roleFrom rawMsg)
            |> Option.defaultValue ""
            |> fun value -> value.ToLowerInvariant()

    let private partTypeKind (part: obj) : string option =
        tryUnboxString part?``type`` |> Option.map (fun t -> t.ToLowerInvariant())

    let private isToolPartKind (kind: string) =
        kind = "tool"
        || kind = "tool-call"
        || kind = "tool_call"
        || kind = "tool-result"
        || kind = "tool_result"

    let private isToolPart (part: obj) : bool =
        if isNull part then
            false
        else
            partTypeKind part |> Option.exists isToolPartKind

    let private statusFromState (state: obj) : string option =
        tryUnboxString state?status
        |> Option.map (fun status -> status.ToLowerInvariant())

    let private partStatus (part: obj) : string option =
        if isNull part then
            None
        else
            tryObj part?state |> Option.bind statusFromState

    let private isPendingToolStatus (status: string option) =
        match status with
        | Some "completed"
        | Some "error" -> false
        | _ -> true

    let private isCompletedToolStatus (status: string option) =
        match status with
        | Some "completed"
        | Some "error" -> true
        | _ -> false

    /// Host raw：pending/running tool part = call；completed/error = result。
    let private isToolCallMessage (rawMsg: obj) : bool =
        rawParts rawMsg
        |> Array.exists (fun part -> isToolPart part && isPendingToolStatus (partStatus part))

    let private isToolResultMessage (rawMsg: obj) : bool =
        rawParts rawMsg
        |> Array.exists (fun part -> isToolPart part && isCompletedToolStatus (partStatus part))

    // ── transcript addressing ────────────────────────────────────────────────

    let private callIdFromRawId (raw: string) : string =
        if raw.EndsWith("-call", StringComparison.Ordinal) then
            raw.Substring(0, raw.Length - 5)
        else
            raw

    let private syntheticCallIdFromInfo (info: obj) : string option =
        tryUnboxString info?pairCallID
        |> Option.orElseWith (fun () -> tryUnboxString info?id |> Option.map callIdFromRawId)

    /// The callId a stripped synthetic message belongs to: the call half's id
    /// is `callId + "-call"`, the result half's id is `callId`.
    let private syntheticCallIdOf (rawMsg: obj) : string option =
        messageInfo rawMsg |> Option.bind syntheticCallIdFromInfo

    let private claimAddress (seen: Set<string>) (message: obj) : Result<string * Set<string>, string> =
        match ProviderWireDecode.hostMessageId message with
        | None -> Error "transcript message without address (HOST-013)"
        | Some id when Set.contains id seen -> Error(sprintf "duplicate transcript address %s (HOST-013)" id)
        | Some id -> Ok(id, Set.add id seen)

    /// Real messages in transcript order, each carrying its Host message
    /// address (`info.id` / `id`). Every real message must have one — a message
    /// without an address could never anchor a synthetic half, and a duplicate
    /// address would make anchors ambiguous.
    let private addressedRealMessages (realMessages: obj list) : Result<(string * obj) list, string> =
        let rec loop acc seen =
            function
            | [] -> Ok(List.rev acc)
            | message :: rest ->
                claimAddress seen message
                |> Result.bind (fun (id, seen') -> loop ((id, message) :: acc) seen' rest)

        loop [] Set.empty realMessages

    // ── replay：唯一合法渲染路径 ─────────────────────────────────────────────

    /// The one and only HOST-013 renderer: real messages + durable anchored
    /// pairs → the exact provider wire. Historical completed rows sit at their
    /// own durable ResultGap; nothing re-decides their position (`historyBlock` 禁止).
    ///
    /// 组内排序唯一合法：`Ordinal` 升序。
    ///
    /// Anchor 不在当前真实消息里的 historical pair **不重放、不报错**。
    /// XWire prefix probe 用 FrozenRecordPrefix 替换已覆盖前缀时会 drop 那些
    /// 消息（CTX-010 `DropLeading`）；被覆盖区里的 pair 属于被替换的前缀，
    /// 不应再注入 rewritten view，更不能因此 AbortSession 杀死 recovery slot。
    /// Durable fact 仍保留；完整 transcript 回来时 anchor 在场即可再 replay。
    let private cursorGuidanceSeparator = "\u0000\uFEFF"

    let private isString (value: obj) : bool =
        not (isNull value) && emitJsExpr value "typeof $0 === 'string'"

    let private terminalGuidanceIndex (index: int) (part: obj) : int option =
        match isToolPart part, partStatus part, tryObj part?state with
        | false, _, _ -> None
        | true, Some "completed", Some state when isString state?output -> Some index
        | true, Some "error", Some state when isString state?error -> Some index
        | true, Some "error", Some state when isString state?output -> Some index
        | _ -> None

    let private applyGuidanceSuffix (suffix: string) (originalPart: obj) (clonedState: obj) =
        let originalState = originalPart?state

        match partStatus originalPart with
        | Some "completed" -> clonedState?output <- box ((unbox<string> originalState?output) + suffix)
        | Some "error" when isString originalState?error ->
            clonedState?error <- box ((unbox<string> originalState?error) + suffix)
        | Some "error" -> clonedState?output <- box ((unbox<string> originalState?output) + suffix)
        | _ -> ()

    let private appendCursorGuidanceToTerminalToolResult (markerTexts: string list) (rawMsg: obj) : obj option =
        let parts = rawParts rawMsg

        match parts |> Array.mapi terminalGuidanceIndex |> Array.choose id |> Array.tryLast with
        | None -> None
        | Some index ->
            let suffix =
                markerTexts
                |> List.map (fun markerText -> cursorGuidanceSeparator + markerText)
                |> String.concat ""

            let originalPart = parts.[index]
            let originalState = originalPart?state
            let clonedState = emitJsExpr originalState "Object.assign({}, $0)"
            applyGuidanceSuffix suffix originalPart clonedState
            let clonedPart = emitJsExpr originalPart "Object.assign({}, $0)"
            clonedPart?state <- clonedState
            let clonedParts = Array.copy parts
            clonedParts.[index] <- clonedPart
            let clonedMessage = emitJsExpr rawMsg "Object.assign({}, $0)"
            clonedMessage?parts <- box clonedParts
            Some clonedMessage

    let private gapPresent (addressSet: Set<string>) (gap: TranscriptGap) =
        match gap with
        | TranscriptGap.Start -> true
        | TranscriptGap.Before address
        | TranscriptGap.After address -> Set.contains (TranscriptMessageAddress.value address) addressSet

    let private bucketAdd
        (table: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (address: TranscriptMessageAddress)
        (pair: PairProgrammingGuidelineWire)
        =
        let key = TranscriptMessageAddress.value address

        match table.TryGetValue key with
        | true, entries -> entries.Add pair
        | false, _ ->
            let entries = ResizeArray<PairProgrammingGuidelineWire>()
            entries.Add pair
            table.[key] <- entries

    let private addAtGap
        (starts: ResizeArray<PairProgrammingGuidelineWire>)
        (before: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (after: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (pair: PairProgrammingGuidelineWire)
        (gap: TranscriptGap)
        =
        match gap with
        | TranscriptGap.Start -> starts.Add pair
        | TranscriptGap.Before address -> bucketAdd before address pair
        | TranscriptGap.After address -> bucketAdd after address pair

    let private placeCursorResultGap
        (cursorAfter: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (pair: PairProgrammingGuidelineWire)
        =
        match pair.ResultGap with
        | TranscriptGap.After address -> bucketAdd cursorAfter address pair
        | _ -> ()

    let private registerPlaceablePair
        (providerId: string option)
        (starts: ResizeArray<PairProgrammingGuidelineWire>)
        (before: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (after: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (cursorAfter: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (pair: PairProgrammingGuidelineWire)
        =
        if isCursorProvider providerId then
            placeCursorResultGap cursorAfter pair
        else
            addAtGap starts before after pair pair.ResultGap

    let private ordered (entries: ResizeArray<PairProgrammingGuidelineWire>) =
        entries |> Seq.sortBy (fun pair -> pair.Ordinal) |> Seq.toList

    let private emitPairs (output: ResizeArray<obj>) (entries: ResizeArray<PairProgrammingGuidelineWire>) =
        for pair in ordered entries do
            output.Add(buildPairMessage pair.CallId pair.MarkerText)

    let private tryBucket (table: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>) (address: string) =
        match table.TryGetValue address with
        | true, entries -> Some entries
        | _ -> None

    let private emitBucketPairs
        (output: ResizeArray<obj>)
        (table: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (address: string)
        =
        tryBucket table address |> Option.iter (emitPairs output)

    let private projectCursorMessage
        (cursorAfter: Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>)
        (address: string)
        (message: obj)
        =
        match tryBucket cursorAfter address with
        | None -> message
        | Some entries ->
            entries
            |> Seq.sortBy (fun pair -> pair.Ordinal)
            |> Seq.map (fun pair -> pair.MarkerText)
            |> Seq.toList
            |> fun markerTexts ->
                appendCursorGuidanceToTerminalToolResult markerTexts message
                |> Option.defaultValue message

    let private replayAddressed
        (providerId: string option)
        (addressed: (string * obj) list)
        (pairs: PairProgrammingGuidelineWire list)
        : obj list =
        let addressSet = addressed |> List.map fst |> Set.ofList

        // Both durable anchors must remain present even though ordinary and
        // Cursor only render at ResultGap. CallGap stays durable for placement
        // identity and reversible Cursor → ordinary replay.
        let placeable =
            pairs
            |> List.filter (fun pair -> gapPresent addressSet pair.CallGap && gapPresent addressSet pair.ResultGap)

        let starts = ResizeArray<PairProgrammingGuidelineWire>()
        let before = Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>()
        let after = Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>()
        let cursorAfter = Dictionary<string, ResizeArray<PairProgrammingGuidelineWire>>()

        for pair in placeable do
            registerPlaceablePair providerId starts before after cursorAfter pair

        let output = ResizeArray<obj>()
        emitPairs output starts

        for address, message in addressed do
            emitBucketPairs output before address
            output.Add(projectCursorMessage cursorAfter address message)
            emitBucketPairs output after address

        Seq.toList output

    let private replay
        (providerId: string option)
        (realMessages: obj list)
        (pairs: PairProgrammingGuidelineWire list)
        : Result<obj list, string> =
        addressedRealMessages realMessages
        |> Result.map (fun addressed -> replayAddressed providerId addressed pairs)

    // ── 本轮新 pair 的 placement（只读当前真实消息）──────────────────────────

    /// 末端结构 → gap：
    ///
    /// - 末端存在同轮 tool batch（`Req1 Req2 Resp1 Resp2 [User]`，或 batch 直接
    ///   结尾）：`After(last call)` / `After(last result)` —— placement identity。
    ///   ordinary 只在 ResultGap 渲染一条 completed Host 行。
    /// - 无 batch 且最后一条消息是 user（trailing user）：`Before(user)` / `Before(user)`。
    /// - 无 batch、无 trailing user：`After(last real)` / `After(last real)`。
    /// - 空 transcript：`Start` / `Start`。
    ///
    /// 新 pair 的 gap 必须落在本次追加区（末尾），否则会改写已发送 wire 的中间字节、
    /// 破坏 append-only prefix。旧实现「pair 总在最后一条 user（任意位置）前」在
    /// continuation transcript（末尾是 assistant 文本）上会在中途插入新 pair —— 正是
    /// 本 Change 修复的 prefix 破坏。
    let rec private takeToolResults found xs =
        match xs with
        | message :: tail when isToolResultMessage message -> takeToolResults (message :: found) tail
        | _ -> found, xs

    let rec private takeToolCalls found xs =
        match xs with
        | message :: tail when isToolCallMessage message -> takeToolCalls (message :: found) tail
        | _ -> found, xs

    let private gapsAfterToolBatch (lastCall: obj) (lastResult: obj) =
        match ProviderWireDecode.hostMessageId lastCall, ProviderWireDecode.hostMessageId lastResult with
        | Some callId, Some resultId ->
            Ok(
                TranscriptGap.After(TranscriptMessageAddress.create callId),
                TranscriptGap.After(TranscriptMessageAddress.create resultId)
            )
        | _ -> Error "tool batch message without transcript address (HOST-013)"

    let private gapsAroundAddress
        (gapCtor: TranscriptMessageAddress -> TranscriptGap)
        (message: obj)
        (errorMsg: string)
        =
        match ProviderWireDecode.hostMessageId message with
        | Some id ->
            let address = TranscriptMessageAddress.create id
            Ok(gapCtor address, gapCtor address)
        | None -> Error errorMsg

    let private decideFromBatchEnds (lastIsUser: bool) (last: obj) (resultRun: obj list) (callRun: obj list) =
        match List.rev resultRun, List.rev callRun with
        | lastResult :: _, lastCall :: _ -> gapsAfterToolBatch lastCall lastResult
        | _ when lastIsUser ->
            gapsAroundAddress TranscriptGap.Before last "trailing user without transcript address (HOST-013)"
        | _ -> gapsAroundAddress TranscriptGap.After last "last message without transcript address (HOST-013)"

    let private decideCurrentPlacement (realMessages: obj list) : Result<TranscriptGap * TranscriptGap, string> =
        match List.rev realMessages with
        | [] -> Ok(TranscriptGap.Start, TranscriptGap.Start)
        | last :: rest ->
            let lastIsUser = messageRole last = "user"
            let scanFrom = if lastIsUser then rest else (last :: rest)
            let resultRun, afterResults = takeToolResults [] scanFrom
            let callRun, _ = takeToolCalls [] afterResults
            decideFromBatchEnds lastIsUser last resultRun callRun

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
        : Task<Result<unit, string>> =
        task {
            let fact =
                HostFact.PairProgrammingGuidelineAnchored
                    {| SessionId = sessionId
                       Ordinal = pair.Ordinal
                       CallId = ToolCallId.create pair.CallId
                       MarkerText = pair.MarkerText
                       CallGap = pair.CallGap
                       ResultGap = pair.ResultGap |}

            match! AgentJournal.appendAgent (StreamId.Session sessionId) None fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private nextGuidelineOrdinal (history: PairProgrammingGuidelineWire list) =
        match history with
        | [] -> 1L
        | pairs -> (List.last pairs).Ordinal + 1L

    let private findPlacement
        (history: PairProgrammingGuidelineWire list)
        (callGap: TranscriptGap)
        (resultGap: TranscriptGap)
        =
        history
        |> List.tryFind (fun pair -> pair.CallGap = callGap && pair.ResultGap = resultGap)

    let private commitPairInjection
        (providerId: string option)
        (history: PairProgrammingGuidelineWire list)
        (append: PairProgrammingGuidelineWire -> Task<Result<unit, string>>)
        (sessionId: string option)
        (markerText: string)
        (realMessages: obj list)
        (callGap: TranscriptGap)
        (resultGap: TranscriptGap)
        : Task<Result<obj list, string>> =
        taskResult {
            let existing = findPlacement history callGap resultGap

            if Option.isSome existing || skipAutoInjectedRequested providerId then
                return! replay providerId realMessages history
            else
                let ordinal = nextGuidelineOrdinal history

                let candidate =
                    { Ordinal = ordinal
                      CallId = stableCallId sessionId ordinal
                      MarkerText = markerText
                      CallGap = callGap
                      ResultGap = resultGap }

                let! rendered = replay providerId realMessages (history @ [ candidate ])
                do! append candidate
                return rendered
        }

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
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Task<Result<obj list, string>> =
        taskResult {
            let key = transcriptKey sessionId

            let strippedCallIds =
                rawMessages
                |> List.filter isPairProgrammingThought
                |> List.choose syntheticCallIdOf

            let lang =
                if markerText.Contains "以" || markerText.Contains "我" || markerText.Contains "结对" then
                    Some ProviderLanguage.SimplifiedChinese
                else
                    Some ProviderLanguage.English

            let realMessages =
                rawMessages
                |> List.filter (isPairProgrammingThought >> not)
                |> sanitizeActiveToolCalls lang

            let providerId = providerIdFromMessages realMessages

            let history, append =
                match journal, sessionId with
                | Some durable, Some sid when not (String.IsNullOrWhiteSpace sid) ->
                    let session = SessionId.create sid
                    readDurableHistory durable session, appendDurable durable session
                | _ -> readMemoryHistory key, (fun pair -> Task.FromResult(appendMemory key pair))

            let knownCallIds = history |> List.map (fun pair -> pair.CallId) |> Set.ofList

            let orphaned =
                strippedCallIds
                |> List.filter (fun callId -> not (Set.contains callId knownCallIds))

            do!
                Result.requireTrue
                    (sprintf
                        "synthetic messages without durable record (callId %s, HOST-013)"
                        (String.Join(", ", orphaned |> List.truncate 3)))
                    (List.isEmpty orphaned)

            let! callGap, resultGap = decideCurrentPlacement realMessages

            return! commitPairInjection providerId history append sessionId markerText realMessages callGap resultGap
        }

    let tryInject
        (journal: AgentJournal option)
        (sessionId: string option)
        (markerText: string)
        (rawMessages: obj list)
        : Task<Result<obj list, string>> =
        tryInjectCore journal sessionId markerText rawMessages
