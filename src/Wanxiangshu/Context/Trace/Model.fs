namespace Wanxiangshu.Context.Trace

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Host
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// 一个 XTrace 语义事实。`Provenance` 保存 session/run/message/part 归属供
/// 证明与恢复使用，renderer 永不输出它（HOST-005）。
type XTraceItem =
    { Cursor: XTraceCursor
      Provenance: string
      Role: string
      Part: SemanticPart }

[<RequireQualifiedAccess>]
module internal WanxiangliTrace =

    [<Literal>]
    let MaxPartChunkBytes = 524288

    [<Import("Trace", "wanxiangli/trace")>]
    let private traceCtor: obj = jsNative

    [<Emit("new $0($1, $2)")>]
    let private createTraceWithRecords (_ctor: obj) (_traceId: string) (_records: string array) : obj = jsNative

    [<Emit("new $0($1)")>]
    let private createEmptyTrace (_ctor: obj) (_traceId: string) : obj = jsNative

    [<Emit("(() => { const wm = new WeakMap(); return { attach: (b, t) => { wm.set(b, t); return b; }, take: (b, c) => { const t = wm.get(b); if (t && t.inspect().cursor === c) { wm.delete(b); return t; } return null; }, peek: (b, c) => { const t = wm.get(b); return (t && t.inspect().cursor === c) ? t : null; } }; })()")>]
    let private createTraceCache () : obj = jsNative

    let private traceCache = createTraceCache ()

    [<Emit("$0.attach($1, $2)")>]
    let private attachCachedTrace (_cache: obj) (_bridge: 'a) (_trace: obj) : 'a = jsNative

    [<Emit("$0.take($1, $2)")>]
    let private takeCachedTrace (_cache: obj) (_bridge: 'a) (_expectedCursor: int) : obj = jsNative

    [<Emit("$0.peek($1, $2)")>]
    let private peekCachedTrace (_cache: obj) (_bridge: 'a) (_expectedCursor: int) : obj = jsNative

    [<Emit("new TextEncoder().encode($0)")>]
    let private utf8ToBytes (_value: string) : obj = jsNative

    [<Emit("new Uint8Array(0)")>]
    let private emptyBytes () : obj = jsNative

    [<Emit("$0.byteLength")>]
    let private bytesLength (_bytes: obj) : int = jsNative

    [<Emit("$0.slice($1, $2)")>]
    let private sliceBytes (_bytes: obj) (_startIdx: int) (_endIdx: int) : obj = jsNative

    [<Emit("$0.capture($1)")>]
    let private captureObs (_trace: obj) (_observation: obj) : obj = jsNative

    [<Emit("$0.head()")>]
    let private rawHead (_trace: obj) : obj = jsNative

    [<Emit("$0.cut($1)")>]
    let private rawCut (_trace: obj) (_cursor: int) : obj = jsNative

    [<Emit("$0.inspect().cursor")>]
    let private rawInspectCursor (_trace: obj) : int = jsNative

    [<Emit("$0.read($1, $2)")>]
    let private rawRead (_trace: obj) (_afterCut: obj) (_throughCut: obj) : obj array = jsNative

    [<Emit("$0[$1]")>]
    let private getField (_target: obj) (_name: string) : obj = jsNative

    type ProjectionBridge =
        {
            TraceId: string
            /// Trace record lines, stored NEWEST-first so each append is an O(1)
            /// prepend. `recordsOldestFirst` reverses to chronological order for
            /// the wanxiangli constructor and the `traceRecords` surface.
            Records: string list
            OpeningCutCursor: int
            /// (sequence, cutCursor) pairs, stored newest-first.
            PartCuts: (int64 * int) list
            RecordCount: int
        }

    /// Oldest-first record lines for the trace constructor / inspection.
    let recordsOldestFirst (bridge: ProjectionBridge) : string list =
        // `Records` is stored newest-first (see append sites); reverse to the
        // chronological order the wanxiangli constructor and inspect surface need.
        List.rev bridge.Records

    let private instantiateTrace (traceId: string) (records: string list) : obj =
        if List.isEmpty records then
            createEmptyTrace traceCtor traceId
        else
            createTraceWithRecords traceCtor traceId (List.toArray records)

    let private acquireWritableTrace (bridge: ProjectionBridge) : obj =
        let cached = takeCachedTrace traceCache bridge bridge.RecordCount

        if not (isNull cached) then
            cached
        else
            instantiateTrace bridge.TraceId (recordsOldestFirst bridge)

    let private acquireReadableTrace (bridge: ProjectionBridge) : obj =
        let cached = peekCachedTrace traceCache bridge bridge.RecordCount

        if not (isNull cached) then
            cached
        else
            let created = instantiateTrace bridge.TraceId (recordsOldestFirst bridge)
            attachCachedTrace traceCache bridge created |> ignore
            created

    let private cutFromJs (raw: obj) : XTraceCut =
        { TraceId = string (getField raw "traceId")
          Cursor = unbox<int> (getField raw "cursor")
          Digest = string (getField raw "digest") }

    let private cutToJs (cut: XTraceCut) : obj =
        createObj
            [ "traceId", box cut.TraceId
              "cursor", box cut.Cursor
              "digest", box cut.Digest ]

    let private chunkBytes (raw: string) : obj array =
        let encoded = utf8ToBytes raw
        let total = bytesLength encoded

        if total = 0 then
            [| emptyBytes () |]
        else
            let count = (total + MaxPartChunkBytes - 1) / MaxPartChunkBytes

            Array.init count (fun index ->
                let startOffset = index * MaxPartChunkBytes
                let endOffset = min total (startOffset + MaxPartChunkBytes)
                sliceBytes encoded startOffset endOffset)

    let private obsOpening (bytes: obj) : obj =
        createObj [ "type", box "Opening"; "bytes", bytes ]

    let private obsOpeningBound (messageId: string) : obj =
        createObj [ "type", box "OpeningBound"; "messageId", box messageId ]

    let private obsMessageObserved (messageId: string) (role: string) (origin: string) (runIdOpt: string option) : obj =
        let runField =
            match runIdOpt with
            | Some id -> box id
            | None -> null

        createObj
            [ "type", box "MessageObserved"
              "messageId", box messageId
              "role", box role
              "origin", box origin
              "runId", runField ]

    let private obsPartConfirmed (messageId: string) (partId: string) (index: int) (kind: string) (bytes: obj) : obj =
        createObj
            [ "type", box "PartConfirmed"
              "messageId", box messageId
              "partId", box partId
              "index", box index
              "kind", box kind
              "bytes", bytes ]

    let private obsMessageFinished (messageId: string) (partCount: int) : obj =
        createObj
            [ "type", box "MessageFinished"
              "messageId", box messageId
              "partCount", box partCount ]

    let private captureObsRecord (trace: obj) (obs: obj) : string =
        let result = captureObs trace obs

        if string (getField result "status") <> "Appended" then
            invalidOp "A new semantic observation must append a new Trace record"

        string (getField result "record")

    let private captureOpeningRecords (trace: obj) (payload: string) : string list =
        let chunks = chunkBytes payload

        if rawInspectCursor trace = 0 && chunks.Length = 1 then
            [ captureObsRecord trace (obsOpening chunks.[0]) ]
        else
            let messageId = "xopening"

            let observed =
                captureObsRecord trace (obsMessageObserved messageId "system" "host-internal" None)

            let parts =
                chunks
                |> Array.mapi (fun index chunk ->
                    captureObsRecord
                        trace
                        (obsPartConfirmed messageId (sprintf "opening:%d" index) index "binary" chunk))
                |> Array.toList

            let finished = captureObsRecord trace (obsMessageFinished messageId chunks.Length)
            let bound = captureObsRecord trace (obsOpeningBound messageId)
            observed :: (parts @ [ finished; bound ])

    let emptyBridge (traceId: string) : ProjectionBridge =
        let trace = createEmptyTrace traceCtor traceId

        let bridge =
            { TraceId = traceId
              Records = []
              OpeningCutCursor = 0
              PartCuts = []
              RecordCount = 0 }

        attachCachedTrace traceCache bridge trace

    let private openingEnvelope (assignment: string) (requirements: string list) : string =
        let reqJoined = String.concat "\n" requirements
        sprintf "assignment:%d:%s\n%s" assignment.Length assignment reqJoined

    let appendOpeningToBridge
        (assignment: string)
        (requirements: string list)
        (bridge: ProjectionBridge)
        : ProjectionBridge =
        let trace = acquireWritableTrace bridge
        let records = captureOpeningRecords trace (openingEnvelope assignment requirements)
        let cursor = rawInspectCursor trace

        let updated =
            { bridge with
                Records = List.fold (fun prior record -> record :: prior) bridge.Records records
                OpeningCutCursor =
                    if bridge.RecordCount = 0 then
                        cursor
                    else
                        bridge.OpeningCutCursor
                RecordCount = cursor }

        attachCachedTrace traceCache updated trace

    let private materialKind =
        function
        | "tool_call" -> "tool-call"
        | "tool_result" -> "tool-result"
        | "media_omitted" -> "media-omitted"
        | "text" -> "text"
        | "reasoning" -> "reasoning"
        | kind -> invalidOp ("Unknown confirmed material kind: " + kind)

    [<Emit("JSON.stringify($0)")>]
    let private encodeIdentity (_value: string) : string = jsNative

    let private identity scope value =
        scope + ":" + HostDigest.sha256Hex (encodeIdentity value)

    let traceIdentity session = identity "xtrace-material-v3" session

    let private captureMaterialRecords trace identity role kind toolName providerRun body =
        let chunks = chunkBytes body

        chunks
        |> Array.mapi (fun index bytes ->
            createObj
                [ "type", box "MaterialConfirmed"
                  "identity", box identity
                  "role", box role
                  "kind", box kind
                  "toolName", toolName |> Option.map box |> Option.toObj
                  "providerRun", providerRun |> Option.map box |> Option.toObj
                  "chunk", box index
                  "chunks", box chunks.Length
                  "bytes", bytes ]
            |> captureObsRecord trace)
        |> Array.toList

    let appendPartToBridge
        (cursorSequence: int64)
        (role: string)
        (provenance: string)
        (kind: string)
        (toolName: string option)
        (providerRun: string option)
        (body: string)
        (bridge: ProjectionBridge)
        : ProjectionBridge =
        let trace = acquireWritableTrace bridge

        let addedRecords =
            captureMaterialRecords trace (identity "part" provenance) role (materialKind kind) toolName providerRun body

        let afterCursor = rawInspectCursor trace

        let updated =
            { bridge with
                Records = List.fold (fun records record -> record :: records) bridge.Records addedRecords
                PartCuts = (cursorSequence, afterCursor) :: bridge.PartCuts
                RecordCount = afterCursor }

        attachCachedTrace traceCache updated trace

    let appendTerminalToBridge
        (providerRun: string)
        (body: string)
        (frontierSequence: int64)
        (bridge: ProjectionBridge)
        : ProjectionBridge =
        let trace = acquireWritableTrace bridge
        let identity = identity "terminal" providerRun

        let frontierCursor =
            bridge.PartCuts
            |> List.tryFind (fun (sequence, _) -> sequence < frontierSequence)
            |> Option.map snd
            |> Option.defaultValue bridge.OpeningCutCursor

        let frontier = rawCut trace frontierCursor

        let material =
            captureMaterialRecords trace identity "assistant" "text" None (Some providerRun) body

        let bound =
            createObj
                [ "type", box "TerminalBound"
                  "identity", box identity
                  "providerRun", box providerRun
                  "frontier", frontier ]
            |> captureObsRecord trace

        let addedRecords = material @ [ bound ]

        let afterCursor = rawInspectCursor trace

        let updated =
            { bridge with
                Records = List.fold (fun records record -> record :: records) bridge.Records addedRecords
                RecordCount = afterCursor }

        attachCachedTrace traceCache updated trace

    let private readMaterialText identity (bridge: ProjectionBridge) =
        let trace = acquireReadableTrace bridge
        let material: obj = trace?material (identity, rawHead trace)

        if isNullOrUndefined material || not (unbox<bool> material?complete) then
            invalidOp ("Confirmed trace material is unavailable: " + identity)

        let text: obj = material?content?text

        if isNullOrUndefined text then
            invalidOp ("Confirmed trace material is not valid UTF-8: " + identity)

        unbox<string> text

    let partText provenance bridge =
        readMaterialText (identity "part" provenance) bridge

    let terminalText providerRun bridge =
        readMaterialText (identity "terminal" providerRun) bridge

    let headCutOfBridge (bridge: ProjectionBridge) : XTraceCut =
        let trace = acquireReadableTrace bridge
        cutFromJs (rawHead trace)

    let private anchorCutCursorBeforeSequence (sequence: int64) (bridge: ProjectionBridge) : int =
        bridge.PartCuts
        |> List.tryFind (fun (seq, _) -> seq < sequence)
        |> Option.map snd
        |> Option.defaultValue bridge.OpeningCutCursor

    let cutAtSequenceOfBridge (sequence: int64) (bridge: ProjectionBridge) : XTraceCut =
        let trace = acquireReadableTrace bridge

        let targetCursor =
            if sequence <= 0L then
                bridge.OpeningCutCursor
            else
                bridge.PartCuts
                |> List.tryFind (fun (seq, _) -> seq <= sequence)
                |> Option.map snd
                |> Option.defaultValue bridge.OpeningCutCursor

        cutFromJs (rawCut trace targetCursor)

    let private intervalCursorsForRange (range: XTraceRange) (bridge: ProjectionBridge) : int * int =
        let startSeq = range |> XTraceRange.startInclusive |> XTraceCursor.sequence
        let endSeq = range |> XTraceRange.endExclusive |> XTraceCursor.sequence

        anchorCutCursorBeforeSequence startSeq bridge, anchorCutCursorBeforeSequence endSeq bridge

    let intervalOfRangeOnBridge (range: XTraceRange) (bridge: ProjectionBridge) : XTraceCut * XTraceCut =
        let trace = acquireReadableTrace bridge
        let afterCursor, throughCursor = intervalCursorsForRange range bridge
        cutFromJs (rawCut trace afterCursor), cutFromJs (rawCut trace throughCursor)

    let intervalAfterCoverageOnBridge (coverage: RecordCoverage) (bridge: ProjectionBridge) : XTraceCut * XTraceCut =
        let trace = acquireReadableTrace bridge

        let ingestedSeq =
            coverage |> RecordCoverage.ingestedThrough |> XTraceCursor.sequence

        let afterCut = cutAtSequenceOfBridge ingestedSeq bridge

        let throughCursor =
            bridge.PartCuts
            |> List.tryHead
            |> Option.map snd
            |> Option.defaultValue bridge.OpeningCutCursor

        let throughCut = cutFromJs (rawCut trace throughCursor)

        let effectiveAfter =
            if afterCut.Cursor > throughCut.Cursor then
                throughCut
            else
                afterCut

        effectiveAfter, throughCut

    let verifyAndReadIntervalOnBridge (range: XTraceRange) (bridge: ProjectionBridge) : obj array =
        let trace = acquireReadableTrace bridge
        let afterCut, throughCut = intervalOfRangeOnBridge range bridge
        rawRead trace (cutToJs afterCut) (cutToJs throughCut)

[<RequireQualifiedAccess>]
module XTrace =

    /// `cursor >= start` 且 `cursor < endExclusive` 的 items。
    let sliceBetween (start: XTraceCursor) (endExclusive: XTraceCursor) (items: XTraceItem list) =
        let range = XTraceRange.create start endExclusive
        items |> List.filter (fun item -> XTraceRange.contains item.Cursor range)

    /// 当前 head：最后一条 item 之后的位置。空轨迹为 origin。
    let head (items: XTraceItem list) : XTraceCursor =
        match items |> List.tryLast with
        | Some item -> XTraceCursor.nextCursor item.Cursor
        | None -> XTraceCursor.originCursor

    /// `cursor >= start` 的全部 items（到 XTrace head）。
    let sliceFrom (start: XTraceCursor) (items: XTraceItem list) =
        let traceHead = head items

        if XTraceCursor.isAfter start traceHead then
            []
        else
            sliceBetween start traceHead items

    /// 从 SemanticMessage 平铺为带 role 的语义 part 序列。
    ///
    /// XTrace 是语义投影、覆盖区间与 terminal capture 的共同唯一 source
    /// （COMPANION-007、COMPANION-012）。同一 segment 的语义解析不得分叉。
    let flatten (messages: SemanticMessage list) : {| Role: string; Part: SemanticPart |} list =
        messages
        |> List.collect (fun message -> message.Parts |> List.map (fun part -> {| Role = message.Role; Part = part |}))

    /// semantic-trace-010 Opening 区间投影：区间本身已是 OpeningMaterial，
    /// 不过滤 raw tool call/result；Opening 边界由 trace 自身捕获的 Opening
    /// 决定，不由 checkpoint 决定。
    let forOpening (items: XTraceItem list) : XTraceItem list = items

    let private rolePrefix (role: string) =
        match role with
        | "user" -> "user"
        | "assistant" -> "assistant"
        | other -> other

    let private renderMedia mediaType =
        match mediaType with
        | Some mediaValue -> sprintf "[media omitted: %s]" mediaValue
        | None -> "[media omitted]"

    let private renderPart (part: SemanticPart) : string =
        match part with
        | SemanticText text -> text
        | SemanticReasoning text -> text
        | SemanticToolCall(name, args) -> sprintf "[tool call] %s %s" name args
        | SemanticToolResult result -> sprintf "[tool result] %s" result
        | SemanticMedia(mediaType, _digest) -> renderMedia mediaType

    /// 一个 item 的单行文本（role 只对 prompt 有意义；assistant 正文不带
    /// role 前缀，避免渲染文本中每行重复身份）。
    let renderItem (item: XTraceItem) : string =
        match item.Part with
        | SemanticText _ -> sprintf "%s: %s" (rolePrefix item.Role) (renderPart item.Part)
        | _ -> renderPart item.Part

    let render (items: XTraceItem list) : string =
        items |> List.map renderItem |> String.concat "\n"
