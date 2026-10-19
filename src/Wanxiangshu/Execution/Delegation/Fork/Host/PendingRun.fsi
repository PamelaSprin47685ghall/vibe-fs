namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: Role
      AuthorityRoot: AuthorityRootUserMessageId
      Work: AdmittedWork option
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable CompletionInFlight: bool
      mutable Finished: bool }

module HostPendingRun =
    val completionSource: unit -> TaskCompletionSource<AgentCompletionOutcome>
