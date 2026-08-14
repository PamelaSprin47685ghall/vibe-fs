namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// DSL-state-combination: physical — subscription handle + finished latch are runtime resources
type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: Role
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable Finished: bool }

module HostPendingRun =
    let completionSource () =
        TaskCompletionSource<AgentCompletionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)
