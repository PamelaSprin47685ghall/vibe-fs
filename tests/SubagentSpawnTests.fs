module Wanxiangshu.Tests.SubagentSpawnTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Subagent
open Wanxiangshu.Shell.SubagentSpawn

let joinReportsEmptyList () =
    equal "joinReports empty" "" (joinReports [])

let runParallelSpawnsPreservesOrderAndJoins () = promise {
    let order = ResizeArray<string>()
    let prompts = [ "alpha"; "beta"; "gamma" ]
    let spawnOne (prompt: string) =
        promise {
            order.Add prompt
            return $"  {prompt}-out  "
        }
    let! joined = runParallelSpawns prompts spawnOne
    equal "spawn order" (order |> Seq.toArray) (prompts |> List.toArray)
    equal "joined trimmed" "alpha-out\n---\nbeta-out\n---\ngamma-out" joined
}

let runParallelSpawnsEmptyList () = promise {
    let! joined = runParallelSpawns [] (fun _ -> Promise.lift "unused")
    equal "empty parallel join" "" joined
}

let run () = promise {
    joinReportsEmptyList ()
    do! runParallelSpawnsPreservesOrderAndJoins ()
    do! runParallelSpawnsEmptyList ()
}