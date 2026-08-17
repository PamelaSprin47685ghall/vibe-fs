namespace Wanxiangshu.Mission.WorkRecord

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection

open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

/// COMPANION-014 / GLORY-074: when Opening closes for a role.
type CommitmentContract =
    /// Manager T1: first accepted todowrite whose planComplete declaration is true.
    | FirstPlanCompleteTodoWrite

type OpeningPolicy =
    | Immediate
    | BlindPlan of CommitmentContract

[<RequireQualifiedAccess>]
module OpeningPolicy =

    /// Manager OpeningPolicy = BlindPlan (GLORY-074).
    let forManager = BlindPlan FirstPlanCompleteTodoWrite

    /// Non-Manager roles close Opening at InitialCharge (Immediate).
    let immediate = Immediate

/// COMPANION-003/014: OpeningMaterial for LWR Opening section.
///
/// Canonical truth = preserved XTrace `[work start, OpeningBoundary)` (COMPANION-014).
/// `AssignmentText` / requirements hold InitialCharge (OpeningPromptCaptured).
/// `ConstitutiveBody` holds BlindPlan interval after InitialCharge through T1
/// call/result (rendered via XTrace.forOpening); empty for Immediate.
type OpeningMaterial =
    { AssignmentText: string
      AuthoritativeRequirements: string list
      ConstitutiveBody: string }

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
///
/// Opening 仍必须 captured（锚点/gap 起点）；本标志只影响渲染段。
/// tool call/result 不得作为 raw 进入 Recent；T1 constitutive 属 Opening。
type LifecycleWorkRecord =
    { Opening: OpeningMaterial
      Frames: string list
      Gap: XTraceItem list }

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
    /// 仅由 `SyntheticToml.comment` 在 wire 注入，避免 `# # Chronicle`。
    /// `includeOpening=false` 时省略 Opening（子→父）。
    /// `Gap` 须已 `forWorkRecord`；render 再次过滤 fail-closed。
    let render (includeOpening: bool) (record: LifecycleWorkRecord) : string =
        let openingBody = if includeOpening then renderOpeningBody record else ""

        let framesText =
            record.Frames
            |> List.filter (System.String.IsNullOrWhiteSpace >> not)
            |> String.concat "\n\n"

        let gapText = record.Gap |> XTrace.forWorkRecord |> XTrace.render

        let sections =
            [ section "Opening" openingBody
              section "Chronicle" framesText
              section "Recent work" gapText ]
            |> List.filter (fun text -> text <> "")

        String.concat "\n\n" sections

    /// 确定性物化。gap 经 `forWorkRecord`；Opening constitutive 经 `forOpening`。
    /// `includeOpening`：父→子 true，子→父 false（EXEC-006）。
    /// `openingEnd` = WorkRecordStart / OpeningBoundary（exclusive）。
    let materialize
        (opening: OpeningMaterial)
        (frames: string list)
        (trace: XTraceItem list)
        (coverage: RecordCoverage)
        (openingEnd: XTraceCursor)
        (includeOpening: bool)
        : string =
        let gapStart =
            { Sequence = max coverage.IngestedThrough.Sequence openingEnd.Sequence }

        let gap = XTrace.sliceFrom gapStart trace |> XTrace.forWorkRecord

        render
            includeOpening
            { Opening = opening
              Frames = frames
              Gap = gap }

    /// Build OpeningMaterial with constitutive BlindPlan body from an XTrace slice.
    let withConstitutive (opening: OpeningMaterial) (constitutiveItems: XTraceItem list) : OpeningMaterial =
        let body = constitutiveItems |> XTrace.forOpening |> XTrace.render

        { opening with ConstitutiveBody = body }
