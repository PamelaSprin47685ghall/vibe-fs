module Wanxiangshu.Tests.SubagentSpawnTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Subagent
open Wanxiangshu.Runtime.SubagentSpawn
open Wanxiangshu.Runtime.Tooling.ToolOutputBatchToml

let private summaryReport (s: string) : SubagentReport = reportFromSummary s

let joinReportsEmptyList () =
    equal "joinReports empty" "" (joinReports [])

let joinReportsCarriesStructuredFields () =
    let report: SubagentReport =
        { iterator = Some "it-1"
          summary = Some "done"
          error = None
          findings = [ "f1"; "f2" ]
          relatedFiles = [ "a.fs" ]
          relatedCode = [ "fn x" ] }

    let joined = joinReports [ report ]
    check "joined uses reports table" (joined.Contains "reports" || joined.Contains "[[reports]]")
    check "joined embeds summary" (joined.Contains "done")
    check "joined embeds finding" (joined.Contains "f1")
    check "joined embeds related_files" (joined.Contains "related_files" && joined.Contains "a.fs")
    check "joined embeds related_code" (joined.Contains "related_code" && joined.Contains "fn x")
    check "joined embeds iterator" (joined.Contains "it-1")

let runParallelSpawnsPreservesOrderAndJoins () =
    promise {
        let order = ResizeArray<string>()
        let prompts = [ "alpha"; "beta"; "gamma" ]

        let spawnOne (prompt: string) =
            promise {
                order.Add prompt
                return summaryReport $"  {prompt}-out  "
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
        let! joined = runParallelSpawns [] (fun _ -> Promise.lift (summaryReport "unused"))
        equal "empty parallel join" "" joined
    }

let run () =
    promise {
        joinReportsEmptyList ()
        joinReportsCarriesStructuredFields ()
        do! runParallelSpawnsPreservesOrderAndJoins ()
        do! runParallelSpawnsEmptyList ()
    }
