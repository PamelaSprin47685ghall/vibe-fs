namespace Wanxiangshu.Context.Trace

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
module XTrace =

    /// `cursor >= start` 且 `cursor < endExclusive` 的 items。
    let sliceBetween (start: XTraceCursor) (endExclusive: XTraceCursor) (items: XTraceItem list) =
        items
        |> List.filter (fun item ->
            XTraceCursor.isAtOrAfter item.Cursor start
            && XTraceCursor.isBefore item.Cursor endExclusive)

    /// `cursor >= start` 的全部 items（到 XTrace head）。
    let sliceFrom (start: XTraceCursor) (items: XTraceItem list) =
        items |> List.filter (fun item -> XTraceCursor.isAtOrAfter item.Cursor start)

    /// 当前 head：最后一条 item 之后的位置。空轨迹为 origin。
    let head (items: XTraceItem list) : XTraceCursor =
        match items |> List.tryLast with
        | Some item -> XTraceCursor.nextCursor item.Cursor
        | None -> XTraceCursor.originCursor

    /// 从 SemanticMessage 平铺为带 role 的语义 part 序列。
    ///
    /// XTrace 是 Y delta、LWR gap、terminal capture 的共同唯一 source
    /// （COMPANION-007、COMPANION-012）。同一 segment 的语义解析不得分叉；
    /// 到 BloggerDelta 与 LWR 的投影各自有损。cursor 由调用方在 append 时赋值。
    let flatten (messages: SemanticMessage list) : {| Role: string; Part: SemanticPart |} list =
        messages
        |> List.collect (fun message -> message.Parts |> List.map (fun part -> {| Role = message.Role; Part = part |}))

    // ── LWR projection ─────────────────────────────────────────────────────
    //
    // COMPANION-003: tool call/result 留在 XTrace 供 Y 压缩；LWR（gap + terminal）
    // 禁止 raw tool。cursor 仍按 XTrace 推进；被剔除的 part 不进入渲染文本。

    /// part 是否允许进入 LWR 渲染（gap / terminal）。
    let isWorkRecordPart (part: SemanticPart) : bool =
        match part with
        | SemanticToolCall _
        | SemanticToolResult _ -> false
        | SemanticText _
        | SemanticReasoning _
        | SemanticMedia _ -> true

    /// LWR 投影：剔除 raw tool call/result，保留 text/reasoning/omission。
    let forWorkRecord (items: XTraceItem list) : XTraceItem list =
        items |> List.filter (fun item -> isWorkRecordPart item.Part)

    /// COMPANION-014 Opening 区间投影：保留 constitutive commitment（含 BlindPlan T1
    /// call/result）。区间本身已是 OpeningMaterial；不过滤 tool。
    let forOpening (items: XTraceItem list) : XTraceItem list = items

    // ── canonical rendering ────────────────────────────────────────────────
    //
    // XTrace 诊断/全量渲染可含 tool；LWR Recent/gap 调用方必须先 `forWorkRecord`。
    // Opening 调用方用 `forOpening`。同一输入 items 必须产生相同文本（COMPANION-012）。

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
    /// role 前缀，避免 LWR 中每行重复身份）。
    let renderItem (item: XTraceItem) : string =
        match item.Part with
        | SemanticText _ -> sprintf "%s: %s" (rolePrefix item.Role) (renderPart item.Part)
        | _ -> renderPart item.Part

    /// 整个 items 的稳定渲染：空 items 为空字符串，非空以单 LF 连接。
    /// LWR 入口必须先 `forWorkRecord`；本函数本身不做 tool 剔除。
    let render (items: XTraceItem list) : string =
        items |> List.map renderItem |> String.concat "\n"
