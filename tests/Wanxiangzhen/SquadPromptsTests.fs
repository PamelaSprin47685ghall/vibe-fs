module Wanxiangshu.Tests.Wanxiangzhen.SquadPromptsTests

open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts

let private taskId = "squad-a1b2"
let private title = "test-title"
let private description = "test-description-body"
let private masterBranch = "main"

open Wanxiangshu.Runtime.Prompt

let private prompt () : string =
    PromptToml.render (buildSlavePromptDocument taskId title description masterBranch)

let private slaveEntries () : (string * (unit -> unit)) list =
    [ ("buildSlavePrompt output renders TOML with SquadWorker agent_role",
       fun () ->
           let p = prompt ()
           checkBare (p.Contains "objective = ")
           checkBare (p.Contains "Wanxiangzhen Slave Agent (mutating)"))

      ("buildSlavePrompt contains taskId and title",
       fun () ->
           let p = prompt ()
           checkBare (p.Contains(sprintf "task %s" taskId))
           checkBare (p.Contains title))

      ("buildSlavePrompt contains submit_to_squad",
       fun () ->
           let p = prompt ()
           checkBare (p.Contains "submit_to_squad"))

      ("buildSlavePrompt contains git rebase + masterBranch",
       fun () ->
           let p = prompt ()
           checkBare (p.Contains(sprintf "git rebase %s" masterBranch)))

      ("buildSlavePrompt contains /loop or With-Review",
       fun () ->
           let p = prompt ()
           checkBare (p.Contains "/loop" || p.Contains "With-Review"))

      ("buildSlavePromptDocument requires research task execution to create report file, enable downstream reading, git add and commit",
       fun () ->
           let doc = buildSlavePromptDocument taskId title description masterBranch
           let rendered = PromptToml.render doc
           let lower = rendered.ToLowerInvariant()
           checkBare (lower.Contains "report")
           checkBare (lower.Contains "workspace-relative" || lower.Contains "relative")
           checkBare (lower.Contains "git add")
           checkBare (lower.Contains "commit" || lower.Contains "git commit")
           checkBare (lower.Contains "subsequent" || lower.Contains "downstream" || lower.Contains "read")) ]

let private coordinatorEntries () : (string * (unit -> unit)) list =
    [ ("buildCoordinatorPromptDocument renders TOML with Coordinator agent_role and requirement",
       fun () ->
           let req = "Implement user authentication subsystem"
           let doc = buildCoordinatorPromptDocument req
           let rendered = PromptToml.render doc
           let view = PromptDocument.view doc
           checkBare (view.agentRole = AgentRole.Coordinator)
           checkBare (rendered.Contains "Coordinator Agent (decomposition-only)")
           checkBare (rendered.Contains req))

      ("buildCoordinatorPromptDocument contains DoNotExecute boundary action and squad_update contract",
       fun () ->
           let req = "Implement payment integration"
           let doc = buildCoordinatorPromptDocument req
           let rendered = PromptToml.render doc
           let view = PromptDocument.view doc
           checkBare (view.boundaries |> List.exists (function PromptBoundary.DoNotExecute _ -> true | _ -> false))
           checkBare (rendered.Contains "action = \"execute\"")
           checkBare (rendered.Contains "squad_update")
           checkBare (rendered.Contains "tasks_created")
           checkBare (not (rendered.Contains "submit_to_squad"))
           checkBare (not (rendered.Contains "git rebase")))

      ("buildCoordinatorPromptDocument requires research task description to specify workspace-relative report path and commit requirements",
       fun () ->
           let req = "Investigate memory leak in actor system"
           let doc = buildCoordinatorPromptDocument req
           let rendered = PromptToml.render doc
           let lower = rendered.ToLowerInvariant()
           checkBare (lower.Contains "research" || lower.Contains "investigat")
           checkBare (lower.Contains "workspace-relative" || lower.Contains "report path" || lower.Contains "relative report path")
           checkBare (lower.Contains "description")
           checkBare (lower.Contains "commit" || lower.Contains "submit")) ]

let entries () : (string * (unit -> unit)) list =
    slaveEntries () @ coordinatorEntries ()
