namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Context.Trace

/// COMPANION-014 / GLORY-074: when Opening closes for a role.
type CommitmentContract =
    /// Manager T1: first accepted todowrite whose planComplete declaration is true.
    | FirstPlanCompleteTodoWrite

type OpeningPolicy =
    | Immediate
    | BlindPlan of CommitmentContract

[<RequireQualifiedAccess>]
module OpeningPolicy =

    /// Non-Manager roles close Opening at InitialCharge (Immediate).
    let immediate = Immediate

/// COMPANION-003: LWR 的唯一物化规则。
///
/// ```text
/// LWR(X) = OpeningMaterial?          // includeOpening 控制是否渲染
///        + Chronicle（Y frames）
///        + Recent work（RawGapFromX，经 forWorkRecord；含最后一条助手文本）
/// ```
///
/// 跨 Session 方向不同（EXEC-006 / EXEC-008）：
/// - 父 → 子：`includeOpening = true`（子未见父任务全文）
/// - 子 → 父：`includeOpening = false`（布置者已知任务，勿回传 Opening）
/// 同 Session prefix replacement 不是第三种 delegation：它用
/// `includeOpening = true` 保留自己的章程，并由 Companion memory 作为同一
/// participant 的既有责任重新注入；不得改写成 `commissioner_record` / `attached_work_record`。
///
/// Opening 仍必须 captured（锚点/gap 起点）；本标志只影响渲染段。
/// tool call/result 不得作为 raw 进入 Recent；T1 constitutive 属 Opening。
type LifecycleWorkRecord =
    { Opening: XTraceOpeningEvidence
      Frames: string list
      Gap: string }

[<RequireQualifiedAccess>]
module LifecycleWorkRecord =

    let private section (heading: string) (body: string) : string =
        if System.String.IsNullOrWhiteSpace body then
            ""
        else
            heading + "\n" + body

    let private renderOpeningBody (record: LifecycleWorkRecord) =
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

        [ record.Opening.AssignmentText; reqText; record.Opening.ConstitutiveBody ]
        |> List.filter (System.String.IsNullOrWhiteSpace >> not)
        |> String.concat "\n"

    /// 稳定 Markdown 渲染。空段整段省略。
    /// 段标题为纯文本（Opening / Chronicle / Recent work）；`# `
    /// 仅由 `LlmFacing` 在 wire 注入 comment plane，避免业务层手工加 `#`。
    /// `includeOpening=false` 时省略 Opening（子→父）。
    /// `Gap` 须已 `forWorkRecord`；render 再次过滤 fail-closed。
    let render (includeOpening: bool) (record: LifecycleWorkRecord) : string =
        let openingBody = if includeOpening then renderOpeningBody record else ""

        let framesText =
            record.Frames
            |> List.filter (System.String.IsNullOrWhiteSpace >> not)
            |> String.concat "\n\n"

        let sections =
            [ section "Opening" openingBody
              section "Chronicle" framesText
              section "Recent work" record.Gap ]
            |> List.filter (fun text -> text <> "")

        String.concat "\n\n" sections

    /// Deterministic composition of trace-owner-rendered evidence.
    /// `includeOpening`：父→子 true，子→父 false（EXEC-006）。
    let materialize
        (opening: XTraceOpeningEvidence)
        (frames: string list)
        (gap: string)
        (includeOpening: bool)
        : string =
        render
            includeOpening
            { Opening = opening
              Frames = frames
              Gap = gap }

    /// Attach the trace owner's canonical rendering of BlindPlan constitutive evidence.
    let withConstitutive (opening: XTraceOpeningEvidence) (constitutiveBody: string) : XTraceOpeningEvidence =
        { opening with
            ConstitutiveBody = constitutiveBody }
