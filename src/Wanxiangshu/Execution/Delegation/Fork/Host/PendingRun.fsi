namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: Role
      StartCursor: XTraceCursor
      Handoff: PreparedDelegationHandoff option
      mutable AuthorityRoot: AuthorityRootUserMessageId option
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable Finished: bool }

module HostPendingRun =
    val completionSource: unit -> TaskCompletionSource<AgentCompletionOutcome>
