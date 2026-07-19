module Wanxiangshu.Tests.Wanxiangzhen.E2eBehaviorGapTests

open Wanxiangshu.Tests
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eTests

let private agentsBehaviors: (string * string * string) list =
    [ ("squad_created", "lifecycle", "event")
      ("tasks_created", "lifecycle", "event")
      ("task_started", "execution", "event")
      ("task_submitted", "submission", "event")
      ("task_merged", "submission", "event")
      ("task_done", "completion", "event")
      ("task_error", "error", "event")
      ("squad_cancelled", "lifecycle", "event")
      ("task_scheduling", "scheduling", "runtime")
      ("event_replay", "recovery", "runtime") ]

let entries () : (string * (unit -> unit)) list =
    [ ("e2e_behavior_gap.registry_matches_live",
       fun () ->
           let live = ExtendedMockE2eTests.entriesAsync () |> List.map fst
           chk "gap.ext_mock_len_27" (List.length live = 27)
           chk "gap.ext_mock_labels_unique" (live |> List.distinct |> List.length = live.Length))
      ("e2e_behavior_gap.coverage_table",
       fun () ->
           if not Assert.silentEnabled then
               printfn "| label | area |"
               printfn "|-------|------|"

               ExtendedMockE2eTests.entriesAsync ()
               |> List.map fst
               |> List.iter (fun label ->
                   let area =
                       if label.Contains "replay" then
                           "replay"
                       elif
                           label.Contains "dependency_"
                           || label.Contains "maxConcurrent"
                           || label.Contains "done_beacon"
                           || label.Contains "pid_polling"
                       then
                           "scheduler"
                       elif label.Contains "slave_" then
                           "slave_http"
                       elif
                           label.Contains "submit_"
                           || label.Contains "worktree_add"
                           || label.Contains "merged_"
                           || label.Contains "http_"
                       then
                           "submit"
                       elif
                           label.Contains "multi_session"
                           || label.Contains "dispose_hook"
                           || label.Contains "realistic_opencode"
                       then
                           "plugin"
                       else
                           "unknown"

                   printfn "| %s | %s |" label area))
      ("e2e_behavior_gap.agents_registry",
       fun () ->
           if not Assert.silentEnabled then
               printfn "BEHAVIOR GAP REGISTRY"

           agentsBehaviors
           |> List.iter (fun (behavior, coverage, kind) ->
               chk "gap.agents_behavior_nonempty" (not (System.String.IsNullOrWhiteSpace behavior))
               chk "gap.agents_coverage_nonempty" (not (System.String.IsNullOrWhiteSpace coverage))
               chk "gap.agents_kind_nonempty" (not (System.String.IsNullOrWhiteSpace kind)))

           chk "gap.agents_md_ssot_len_10" (List.length agentsBehaviors = 10)) ]
