namespace Wanxiangshu.Sphinx.V2.Hosts

open Wanxiangshu.Sphinx.V2.Core

/// The MCP tool contract: one strict decoder per tool.
///
/// WHAT[sphinx-v2-036]: the seven public tools and their argument shapes.
/// Required arguments are not defaulted; replacementText is optional.
///
/// WHAT[sphinx-v2-018]: a work submit decodes the answer bytes and nothing else. A
/// certificate patch, a budget debit, an event write or a goal revision in the same
/// call is refused by name, because dropping it silently would let a worker believe
/// it changed something it did not.
/// A refusal the caller can read. It reports that the call changed nothing and never
/// carries a business fact of its own.
type ToolRefusal =
    { Code: string
      Path: string
      Message: string }

type StartArgs =
    { CommandId: string
      GoalText: string
      Constraints: string list
      MaterialRefs: string list
      AuthorizationRef: string
      ProfileRef: string }

type WorkNextArgs =
    { CommandId: string
      InquiryId: string
      Limit: int }

type WorkSubmitArgs =
    { CommandId: string
      InquiryId: string
      WorkId: string
      Attempt: Attempt
      Fence: Fence
      CanonicalResult: string
      ResultSchema: SchemaRef
      ClusterId: string }

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

type GoalAmendArgs =
    { CommandId: string
      InquiryId: string
      AuthorizedBy: string
      ExpectedRevision: string
      AddedConstraints: string list
      ReplacementText: string option }

[<RequireQualifiedAccess>]
module Tool =

    /// 尚未接通 Runtime 操作的拒绝，不把局部 body 解码当作持久化往返证明。
    val unsupported: tool: string -> ToolRefusal

    /// The refusal export returns: this adapter reads published state, so it cannot
    /// enumerate accepted envelopes and therefore cannot state a truthful trace hash.
    val traceUnavailable: tool: string -> ToolRefusal

    /// The refusal fields, read through the module rather than through a compiled
    /// record layout, so a test asserts the contract instead of the representation.
    val refusalCode: ToolRefusal -> string
    val refusalPath: ToolRefusal -> string
    val refusalMessage: ToolRefusal -> string

    val decodeStart: obj -> Result<StartArgs, ToolRefusal>
    val decodeWorkNext: obj -> Result<WorkNextArgs, ToolRefusal>
    val decodeWorkSubmit: obj -> Result<WorkSubmitArgs, ToolRefusal>
    /// 只读入参拒绝明确的附带命令、目标/预算/证书/事件变动；不是未知键全拒绝。
    /// 解码结果不证明 Runtime 的 lease、model 或业务状态副作用。
    val decodeStatus: obj -> Result<StatusArgs, ToolRefusal>
    val decodeCancel: obj -> Result<CancelArgs, ToolRefusal>
    /// 与 status 相同的只读 ingress 边界，另验证 summary/full 模式。
    val decodeExport: obj -> Result<ExportArgs, ToolRefusal>
    val decodeGoalAmend: obj -> Result<GoalAmendArgs, ToolRefusal>
