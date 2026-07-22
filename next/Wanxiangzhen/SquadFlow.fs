namespace Wanxiangshu.Next.Wanxiangzhen

open System
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Session

module SquadFlows =

    let squadProgress: ProgressGuard<SquadScript, SquadError> =
        { Stamp = fun _ -> DateTimeOffset.UtcNow.Ticks
          NoProgress = fun msg -> SquadError.SquadNoProgress msg }

    let squad = FlowBuilder<SquadScript, SquadError>(Some squadProgress)

    let prepareTask (z: SquadScript) (taskInfo: SquadTask) : SquadFlow<VerifiedResult> =
        squad {
            use! worktree = z.CreateWorktree(taskInfo)
            use! slave = z.StartSlave worktree taskInfo

            let! res =
                Flow.create (fun _ ct ->
                    task {
                        let dummyChildScript: ChildScript =
                            { GetOrCreateSession = fun _ -> ChildFlows.child { return slave } }

                        let! r = Flow.run dummyChildScript ct (slave.Run(taskInfo.Prompt))

                        match r with
                        | Ok childRes -> return Ok childRes
                        | Error err -> return Error(SquadError.SquadExecutionError(sprintf "%A" err))
                    })

            let! taskRes = z.Verify(res)
            return! z.PublishVerified worktree taskRes
        }

    let runSquad (z: SquadScript) (plan: SquadPlan) : SquadFlow<SquadOutcome> =
        squad {
            for wave in plan.Waves do
                let! verified = z.RunParallel wave.Tasks (prepareTask z)

                for item in z.MergeOrder(verified) do
                    do! z.FastForward(item)

                do! z.AcceptWave(verified)

            return! z.Complete()
        }
