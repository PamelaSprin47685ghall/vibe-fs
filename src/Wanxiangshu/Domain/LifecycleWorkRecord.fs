namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.ProviderProjection

/// COMPANION-014 / GLORY-074: when Opening closes for a role.
/// BlindPlan wiring (T1 todowrite) is Phase 10; Manager default is BlindPlan stub.
type CommitmentContract =
    /// Manager T1: first accepted `todowrite` on this Life (TODO-015).
    | FirstAcceptedTodoWrite

type OpeningPolicy =
    | Immediate
    | BlindPlan of CommitmentContract

[<RequireQualifiedAccess>]
module OpeningPolicy =

    /// Manager OpeningPolicy = BlindPlan (GLORY-074).
    let forManager = BlindPlan FirstAcceptedTodoWrite

    /// Non-Manager roles close Opening at InitialCharge (Immediate).
    let immediate = Immediate

/// COMPANION-003/014: OpeningMaterial for LWR Opening section.
///
/// Canonical truth = preserved XTrace `[work start, OpeningBoundary)` (COMPANION-014).
/// Capture still lands InitialCharge as assignment (+ fork requirements) via journal
/// `OpeningPromptCaptured`; render must not invent a second reconstruction path.
/// BlindPlan constitutive interval (T1 call/result) is Phase 10 / XTrace.forOpening.
type OpeningMaterial =
    { AssignmentText: string
      AuthoritativeRequirements: string list }

/// COMPANION-003: LWR 的唯一物化规则。
///
/// ```text
/// LWR(X) = OpeningMaterial?          // includeOpening 控制是否渲染
///        + Chronicle（Y frames）
///        + Recent work（RawGapFromX，经 forWorkRecord）
///        + Closing report（TerminalOutputRaw）
/// ```
///
/// 跨 Session 方向不同（EXEC-006 / EXEC-008）：
/// - 父 → 子：`includeOpening = true`（子未见父任务全文）
/// - 子 → 父：`includeOpening = false`（布置者已知任务，勿回传 Opening）
///
/// Opening 仍必须 captured（锚点/gap 起点）；本标志只影响渲染段。
/// tool call/result 不得作为 raw 进入 LWR（T1 constitutive material 属 Opening，非 Recent）。
type LifecycleWorkRecord =
    { Opening: OpeningMaterial
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
    /// 段标题为纯文本（Opening / Chronicle / Recent work / Closing report）；`# `
    /// 仅由 `SyntheticToml.comment` 在 wire 注入，避免 `# # Chronicle`。
    /// `includeOpening=false` 时省略 Opening（子→父）。
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
            [ section "Opening" openingBody
              section "Chronicle" framesText
              section "Recent work" gapText
              section "Closing report" (record.Terminal |> Option.defaultValue "") ]
            |> List.filter (fun text -> text <> "")

        String.concat "\n\n" sections

    /// 确定性物化。gap/terminal 经 `forWorkRecord`。
    /// `includeOpening`：父→子 true，子→父 false（EXEC-006）。
    let materialize
        (opening: OpeningMaterial)
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
