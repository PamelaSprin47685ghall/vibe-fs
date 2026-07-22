module Wanxiangshu.Tests.Wanxiangzhen.SquadPromptsTests

open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts

let private taskId = "squad-a1b2"
let private title = "test-title"
let private description = "test-description-body"
let private masterBranch = "main"

open Wanxiangshu.Runtime.Prompt

let private prompt () : string =
    PromptToml.render (buildSlavePromptDocument taskId title description masterBranch)

let entries () : (string * (unit -> unit)) list =
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
           checkBare (p.Contains "/loop" || p.Contains "With-Review")) ]
