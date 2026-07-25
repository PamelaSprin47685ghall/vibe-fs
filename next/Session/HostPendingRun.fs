namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode

type PendingHostRun =
    { Token: obj
      AgentId: string
      ChildId: SessionId
      Source: TaskCompletionSource<Result<string, string>>
      OutputWatermark: int option
      FallbackOutputCount: int
      mutable Subscription: IDisposable option
      mutable Ready: bool
      mutable Finished: bool }

module HostPendingRun =
    let completionSource () =
        TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

    let resolveModel resolver journal childId =
        match resolver, journal with
        | Some resolver, Some journal ->
            ModelResolver.resolveForSession resolver childId (AgentJournal.snapshot journal)
        | _ -> None
