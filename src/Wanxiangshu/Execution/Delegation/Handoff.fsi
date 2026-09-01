namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type DelegationHandoffWindow = { Range: XTraceRange; IsInitial: bool }

type PreparedDelegationHandoff =
    { Route: DelegationHandoffRoute
      ParentStartInclusive: XTraceCursor
      ParentRecord: string option
      ParentEndExclusive: XTraceCursor }

type ReusableHandoffPort =
    { Prepare: SessionId -> DelegationHandoffRoute -> Task<PreparedDelegationHandoff>
      CheckpointCompleted: SessionId -> PreparedDelegationHandoff -> Task<Result<unit, string>> }

[<RequireQualifiedAccess>]
module DelegationHandoff =
    val key: parent: SessionId -> route: DelegationHandoffRoute -> string
    val window: previousEnd: XTraceCursor option -> currentEnd: XTraceCursor -> DelegationHandoffWindow
    val childRange: startInclusive: XTraceCursor -> endExclusive: XTraceCursor -> XTraceRange
    val promptDocument: charge: string -> parentRecord: string option -> LlmFacing.Document
    val renderPrompt: charge: string -> parentRecord: string option -> string
    val appendParentDelta: providerPrompt: LlmFacing.Document -> parentRecord: string option -> LlmFacing.Document
