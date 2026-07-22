namespace Wanxiangshu.Next.Wanxiangzhen

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Session

type SquadTask =
    { TaskId: string
      TargetAgent: string
      Prompt: string }

type SquadWave =
    { WaveIndex: int
      Tasks: SquadTask list }

type SquadPlan = { Waves: SquadWave list }

type VerifiedResult =
    { TaskId: string
      Result: SquadTaskResult }

type SquadOutcome =
    | SquadCompleted of summary: string
    | SquadFailed of error: string

type SquadScript =
    { CreateWorktree: SquadTask -> SquadFlow<IAsyncDisposable>
      StartSlave: IAsyncDisposable -> SquadTask -> SquadFlow<ChildSession>
      Verify: Wanxiangshu.Next.Session.ChildResult -> SquadFlow<SquadTaskResult>
      PublishVerified: IAsyncDisposable -> SquadTaskResult -> SquadFlow<VerifiedResult>
      MergeOrder: VerifiedResult list -> VerifiedResult list
      FastForward: VerifiedResult -> SquadFlow<unit>
      AcceptWave: VerifiedResult list -> SquadFlow<unit>
      Complete: unit -> SquadFlow<SquadOutcome>
      RunParallel: SquadTask list -> (SquadTask -> SquadFlow<VerifiedResult>) -> SquadFlow<VerifiedResult list> }

and SquadError =
    | SquadNoProgress of string
    | SquadCancelled
    | SquadExecutionError of string

and SquadFlow<'a> = Flow<SquadScript, SquadError, 'a>
