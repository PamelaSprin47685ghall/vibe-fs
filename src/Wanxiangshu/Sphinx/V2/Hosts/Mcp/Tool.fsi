namespace Wanxiangshu.Sphinx.V2.Hosts

open Wanxiangshu.Sphinx.V2.Core

/// The MCP tool contract: one strict decoder per tool.
///
/// WHAT[sphinx-v2-036]: five public tools. Start creates the inquiry and dispatches the
/// first executable step; submit returns the step the caller holds; status reads the
/// inquiry and its next step; cancel stops the inquiry and closes outstanding work;
/// export reads the accepted trace. There is no separate claim tool and no unconnected
/// amendment tool: a capability that has no driver is not advertised.
///
/// WHAT[sphinx-v2-018]: a work submit decodes the answer bytes, the outcome and nothing
/// else. A certificate patch, a budget debit, an event write or a goal revision in the
/// same call is refused by name, because dropping it silently would let a worker believe
/// it changed something it did not.
/// A refusal the caller can read. It reports that the call changed nothing and never
/// carries a business fact of its own.
type ToolRefusal =
    { Code: string
      Path: string
      Message: string }

/// One step of the plan the user selected. The Runtime checks its shape and its
/// resources; it never re-ranks it.
type PlanStep =
    { Id: string
      Title: string
      DependsOn: string list }

type StartArgs =
    { CommandId: string
      GoalText: string
      Constraints: string list
      MaterialRefs: string list
      AuthorizationRef: string
      ProfileRef: string
      Steps: PlanStep list }

/// What the worker reports about the step it holds.
[<RequireQualifiedAccess>]
type WorkOutcome =
    | Succeeded
    | Failed

type WorkSubmitArgs =
    { CommandId: string
      InquiryId: string
      WorkId: string
      Attempt: Attempt
      Fence: Fence
      CanonicalResult: string
      Outcome: WorkOutcome
      Reason: string option }

type StatusArgs = { InquiryId: string }

type CancelArgs =
    { CommandId: string
      InquiryId: string
      Reason: string }

[<RequireQualifiedAccess>]
type ExportMode =
    | Summary
    | Full

type ExportArgs = { InquiryId: string; Mode: ExportMode }

[<RequireQualifiedAccess>]
module Tool =

    /// The refusal fields, read through the module rather than through a compiled
    /// record layout, so a test asserts the contract instead of the representation.
    val refusalCode: ToolRefusal -> string
    val refusalPath: ToolRefusal -> string
    val refusalMessage: ToolRefusal -> string

    val decodeStart: obj -> Result<StartArgs, ToolRefusal>
    val decodeWorkSubmit: obj -> Result<WorkSubmitArgs, ToolRefusal>
    /// 只读入参拒绝明确的附带命令、目标/预算/证书/事件变动；不是未知键全拒绝。
    /// 解码结果不证明 Runtime 的 lease、model 或业务状态副作用。
    val decodeStatus: obj -> Result<StatusArgs, ToolRefusal>
    val decodeCancel: obj -> Result<CancelArgs, ToolRefusal>
    /// 与 status 相同的只读 ingress 边界，另验证 summary/full 模式。
    val decodeExport: obj -> Result<ExportArgs, ToolRefusal>
