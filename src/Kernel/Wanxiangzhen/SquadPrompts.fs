module Wanxiangshu.Kernel.Wanxiangzhen.SquadPrompts

open System
open Wanxiangshu.Kernel.Prompt

let buildCoordinatorPromptDocument (requirement: string) : PromptDocument =
    let bg =
        if String.IsNullOrWhiteSpace requirement then
            None
        else
            Some(requirement.Trim())

    let targets =
        if String.IsNullOrWhiteSpace requirement then
            []
        else
            [ PromptTarget.EvidenceTarget("requirement", requirement.Trim()) ]

    let docView: PromptDocumentView =
        { objective = "Decompose the requirement into a valid squad DAG and submit it."
          background = bg
          agentRole = AgentRole.Coordinator
          targets = targets
          boundaries =
            [ PromptBoundary.DoNotExecute(
                "implementing, modifying files, searching, browsing, testing, or running commands for the requirement"
              ) ]
          rules =
            [ PromptRule.Contract
                "Call squad_update exactly once with a tasks_created event containing tasks[]."
              PromptRule.Contract
                "Each task must be independently executable, have title, description, and dependsOn."
              PromptRule.Contract "Dependencies in dependsOn must refer to valid task IDs."
              PromptRule.Contract
                "For any research/investigation task, description must explicitly state a workspace-relative report path (e.g. `research/<task-id>-<slug>.md`) for downstream or subsequent tasks to read."
              PromptRule.Contract
                "Do not create research/investigation tasks without specifying a workspace-relative report path in description, as research reports must be committed for downstream reading."
              PromptRule.Contract "Stop execution immediately after successful squad_update submission." ]
          outcomes =
            [ { label = "squad_update"
                text = "Task DAG successfully created and submitted." } ] }

    match PromptDocument.create docView with
    | Ok doc -> doc
    | Error errs -> failwithf "Failed to create Coordinator PromptDocument: %A" errs

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
              PromptRule.Contract
                "If task is research or investigation with a workspace-relative report path specified in description, create the report file in repository so downstream or subsequent tasks can read it."
              PromptRule.Contract "After development, call submit_review for review."
              PromptRule.Contract
                "After review PASS, git add the report file and other modified files, git commit, then call submit_to_squad."
              PromptRule.Contract "If review REJECT, fix per feedback and re-review until PASS."
              PromptRule.Contract(sprintf "If asked to rebase, run: git rebase %s, then resubmit." masterBranch) ]
          outcomes =
            [ { label = "submit_to_squad"
                text = "Task is completed, reviewed, committed, and submitted to squad." } ] }

    match PromptDocument.create docView with
    | Ok doc -> doc
    | Error errs -> failwithf "Failed to create SquadWorker PromptDocument: %A" errs
