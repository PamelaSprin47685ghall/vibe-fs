namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.ProviderProjection

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
/// LWR(X) = OpeningPromptRaw?          // includeOpening 控制是否渲染
///        + CompressedMiddleFromY
///        + RawGapFromX（经 forWorkRecord）
///        + TerminalOutputRaw
/// ```
///
/// 跨 Session 方向不同（EXEC-006 / EXEC-008）：
/// - 父 → 子：`includeOpening = true`（子未见父任务全文）
/// - 子 → 父：`includeOpening = false`（布置者已知任务，勿回传 Opening）
///
/// Opening 仍必须 captured（锚点/gap 起点）；本标志只影响渲染段。
/// tool call/result 不得作为 raw 进入 LWR。
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

    /// 稳定 Markdown 渲染。空段整段省略。
    /// 段标题为纯文本（Opening task / Work log / …）；`# ` 仅由
    /// `SyntheticToml.comment` 在 wire 注入，避免 `# # Work log`。
    /// `includeOpening=false` 时省略 Opening task（子→父）。
    /// `Gap` 须已 `forWorkRecord`；render 再次过滤 fail-closed。
    let render (includeOpening: bool) (record: LifecycleWorkRecord) : string =
        let openingBody =
            if not includeOpening then
                ""
            else
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
            [ section "Opening task" openingBody
              section "Work log" framesText
              section "Uncompressed tail" gapText
              section "Final output" (record.Terminal |> Option.defaultValue "") ]
            |> List.filter (fun text -> text <> "")

        String.concat "\n\n" sections

    /// 确定性物化。gap/terminal 经 `forWorkRecord`。
    /// `includeOpening`：父→子 true，子→父 false（EXEC-006）。
    let materialize
        (opening: OpeningPromptRaw)
        (frames: string list)
        (trace: XTraceItem list)
        (coverage: RecordCoverage)
        (openingEnd: XTraceCursor)
        (terminalItems: XTraceItem list)
        (includeOpening: bool)
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

        let terminalText =
            terminalForLwr
            |> List.map (fun item ->
                match item.Part with
                | SemanticText text -> text
                | SemanticReasoning text -> text
                | _ -> XTrace.renderItem item)
            |> String.concat "\n"

        render
            includeOpening
            { Opening = opening
              Frames = frames
              Gap = gap
              Terminal =
                if List.isEmpty terminalForLwr then
                    None
                else
                    Some terminalText }
