namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Domain.ProviderProjection

/// COMPANION-003: Session 首条任务 prompt 原文。永不送 Y 压缩。
///
/// - 根 Human Session：被 Host 接受的第一条 HumanRoot prompt 文本。
/// - fork child：fork 原始 assignment 与权威 requirements。
/// - 不 Trim 正文、不重排、不摘要、不修正语法、不 TOML round-trip。
/// - 不包含 system prompt、transport instruction、`parent_work_record`、run ID、
///   directory 等注入内容（否则多代 fork 递归嵌套、指数膨胀，EXEC-006）。
type OpeningPromptRaw =
    { AssignmentText: string
      AuthoritativeRequirements: string list }

/// COMPANION-003: LWR 的唯一物化规则。
///
/// ```text
/// LWR(X) = OpeningPromptRaw
///        + CompressedMiddleFromY（全部有效 frame，恰好一次）
///        + RawGapFromX（Y 尚未覆盖的 X suffix，经 forWorkRecord）
///        + TerminalOutputRaw（formal text + host-visible reasoning）
/// ```
///
/// tool call/result 是 Y 的压缩来源，不得作为 raw 进入 LWR。相邻 segment
/// 不重复；同一 projection state 产生相同 bytes；物化不触发 LLM、不写新 Y
/// frame、不改变 coverage。
type LifecycleWorkRecord =
    { Opening: OpeningPromptRaw
      Frames: string list
      Gap: XTraceItem list
      Terminal: string option }

[<RequireQualifiedAccess>]
module LifecycleWorkRecord =

    let private section (heading: string) (body: string) : string =
        if System.String.IsNullOrWhiteSpace body then
            ""
        else
            heading + "\n" + body

    /// 稳定 Markdown 渲染。空段整段省略，不输出空标题。
    /// `Gap` 调用方必须已 `XTrace.forWorkRecord`；render 再次过滤作 fail-closed。
    let render (record: LifecycleWorkRecord) : string =
        let openingBody =
            let requirements =
                record.Opening.AuthoritativeRequirements
                |> List.filter (System.String.IsNullOrWhiteSpace >> not)

            let reqText =
                if List.isEmpty requirements then
                    ""
                else
                    requirements
                    |> List.mapi (fun index text -> sprintf "%d. %s" (index + 1) text)
                    |> String.concat "\n"

            [ record.Opening.AssignmentText; reqText ]
            |> List.filter (System.String.IsNullOrWhiteSpace >> not)
            |> String.concat "\n"

        let framesText =
            record.Frames
            |> List.filter (System.String.IsNullOrWhiteSpace >> not)
            |> String.concat "\n\n"

        let gapText = record.Gap |> XTrace.forWorkRecord |> XTrace.render

        let sections =
            [ section "# Opening task" openingBody
              section "# Work log" framesText
              section "# Uncompressed tail" gapText
              section "# Final output" (record.Terminal |> Option.defaultValue "") ]
            |> List.filter (fun text -> text <> "")

        String.concat "\n\n" sections

    /// 确定性物化。`terminalStart` 之前的 XTrace 属 gap；terminal 自身独立。
    ///
    /// `openingEnd` 是 Opening 段在 XTrace 中的结束 cursor（方案 4.1：Y 的
    /// digest cursor 起点设在 Opening 之后）。gap 起点 = max(ingestedThrough,
    /// openingEnd)——Y 从未成功时 coverage 在 origin，gap 仍从 opening 之后
    /// 开始，Opening 不会重复出现于 gap（方案 4.4）。
    ///
    /// COMPANION-003：gap 与 terminal 均经 `forWorkRecord`，剔除 raw tool。
    let materialize
        (opening: OpeningPromptRaw)
        (frames: string list)
        (trace: XTraceItem list)
        (coverage: RecordCoverage)
        (openingEnd: XTraceCursor)
        (terminalItems: XTraceItem list)
        : string =
        let gapStart =
            { Sequence = max coverage.IngestedThrough.Sequence openingEnd.Sequence }

        let gap =
            XTrace.sliceFrom gapStart trace
            |> List.filter (fun item ->
                terminalItems
                |> List.forall (fun terminal -> terminal.Cursor.Sequence <> item.Cursor.Sequence))
            |> XTrace.forWorkRecord

        let terminalForLwr = terminalItems |> XTrace.forWorkRecord

        // Final output：原文，不带 role 前缀。terminal 路径通常已是
        // partsSessionText（text + reasoning）；此处再过滤 tool 作 fail-closed。
        let terminalText =
            terminalForLwr
            |> List.map (fun item ->
                match item.Part with
                | SemanticText text -> text
                | SemanticReasoning text -> text
                | _ -> XTrace.renderItem item)
            |> String.concat "\n"

        render
            { Opening = opening
              Frames = frames
              Gap = gap
              Terminal =
                if List.isEmpty terminalForLwr then
                    None
                else
                    Some terminalText }
