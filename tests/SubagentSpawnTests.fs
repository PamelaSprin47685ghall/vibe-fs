module Wanxiangshu.Tests.SubagentSpawnTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.SubagentSpawn

let joinReportsEmptyList () =
    equal "joinReports empty" "" (joinReports [])

let runParallelSpawnsPreservesOrderAndJoins () =
    promise {
        let order = ResizeArray<string>()
        let prompts = [ "alpha"; "beta"; "gamma" ]

        let spawnOne (prompt: string) =
            promise {
                order.Add prompt
                return $"  {prompt}-out  "
            }

        let! joined = runParallelSpawns prompts spawnOne
        equal "spawn order" (order |> Seq.toArray) (prompts |> List.toArray)
        check "joined uses reports table" (joined.Contains "reports" || joined.Contains "[[reports]]")
        check "joined embeds alpha" (joined.Contains "alpha-out")
        check "joined embeds beta" (joined.Contains "beta-out")
        check "joined embeds gamma" (joined.Contains "gamma-out")
        check "joined no markdown divider" (not (joined.Contains "\n---\n"))
    }

let runParallelSpawnsEmptyList () =
    promise {
        let! joined = runParallelSpawns [] (fun _ -> Promise.lift "unused")
        equal "empty parallel join" "" joined
    }

let run () =
    promise {
        joinReportsEmptyList ()
        do! runParallelSpawnsPreservesOrderAndJoins ()
        do! runParallelSpawnsEmptyList ()
    }
