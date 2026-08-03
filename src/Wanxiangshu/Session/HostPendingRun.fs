namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.OpenCode

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Role: AgentRole
      Source: TaskCompletionSource<AgentCompletionOutcome>
      mutable Subscription: IDisposable option
      mutable Ready: bool
      mutable Finished: bool }

module HostPendingRun =
    let completionSource () =
        TaskCompletionSource<AgentCompletionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

    /// 0.5.0: provider retry count never kills a Logical Run. Kept for call-site
    /// compatibility; always returns None for retry-count death.
    let sessionDeadRefusal (_journal: AgentJournal option) (_childId: SessionId) : string option = None
