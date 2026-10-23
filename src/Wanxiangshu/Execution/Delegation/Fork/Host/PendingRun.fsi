namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: Role
      AuthorityRoot: AuthorityRootUserMessageId
      Work: AdmittedWork option
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable Finished: bool
      Snapshot: ISessionSnapshotPort option }

module HostPendingRun =
    val completionSource: unit -> TaskCompletionSource<AgentCompletionOutcome>
