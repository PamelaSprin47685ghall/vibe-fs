module Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts

open System
open Wanxiangshu.Kernel.Prompt

let buildSlavePromptDocument
    (taskId: string)
    (title: string)
    (description: string)
    (masterBranch: string)
    : PromptDocument =
    let bg =
        if String.IsNullOrWhiteSpace description then
            None
        else
            Some(description.Trim())

    let docView: PromptDocumentView =
        { objective = sprintf "Execute squad task %s: %s" taskId title
          background = bg
          agentRole = AgentRole.SquadWorker
          targets = [ PromptTarget.TodoTarget(sprintf "task %s: %s" taskId title) ]
          boundaries = []
          rules =
            [ PromptRule.Contract "Complete the task following the review workflow."
              PromptRule.Contract "Activate With-Review Mode by following the review workflow below."
              PromptRule.Contract "After development, call submit_review for review."
              PromptRule.Contract "After review PASS, git commit, then call submit_to_squad."
              PromptRule.Contract "If review REJECT, fix per feedback and re-review until PASS."
              PromptRule.Contract(sprintf "If asked to rebase, run: git rebase %s, then resubmit." masterBranch) ]
          outcomes =
            [ { label = "submit_to_squad"
                text = "Task is completed, reviewed, committed, and submitted to squad." } ] }

    match PromptDocument.create docView with
    | Ok doc -> doc
    | Error errs -> failwithf "Failed to create SquadWorker PromptDocument: %A" errs
