module Wanxiangshu.Runtime.PromptFragments

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

/// Atomic read-only constraints (natural-language rules, no PREFIX: prose markers).
let readOnlyConstraintsFor (host: Host) : PromptRule list =
    [ PromptRule.Constraint "Do not write, edit, patch, or create files."
      PromptRule.Constraint "Do not run commands or call mutating tools."
      PromptRule.Constraint("Do not call " + todoWritePromptName host + " or equivalent task-mutation tools.")
      PromptRule.Constraint "Do not change workspace state; output reports only." ]

let readOnlyConstraints = readOnlyConstraintsFor opencode

/// Atomic evaluation criteria — one Criterion per concern (no PREFIX: markers).
let reviewCriteriaItems: string list =
    [ "Use language features fully; prefer correct algorithms and data structures."
      "Keep complexity minimal; remove garbage, dead, legacy-wrapper, and workaround code."
      "Prefer elegant structure free of redundancy."
      "Avoid oversized files, overly long functions, and avoidable complexity."
      "Ensure necessary unit or integration tests exist."
      "Reject design flaws, logic errors, and best-practice violations."
      "Result should feel natural and intuitive for the user or caller."
      "Fully satisfy the original task without cutting corners." ]

let reviewCriteriaRules: PromptRule list =
    reviewCriteriaItems |> List.map PromptRule.Criterion

let todoNudgePromptDocument (todos: string list) : PromptDocument =
    let todoTargets = todos |> List.map PromptTarget.TodoTarget

    let view =
        { objective = "Continue incomplete work and finish the next pending todo"
          background = Some "The stream ended while work remained open"
          agentRole = AgentRole.NudgeSupervisor
          targets = todoTargets
          boundaries = []
          rules = [ PromptRule.Policy "Mark work in progress before editing and complete only after verification." ]
          outcomes =
            [ { label = "continue"
                text = "Resume the next pending work item instead of ending the session." } ] }

    match PromptDocument.create view with
    | Ok doc -> doc
    | Error errs -> failwithf "Invalid todo nudge PromptDocument: %A" errs

let todoNudgePromptFor (todos: string list) : string =
    PromptToml.render (todoNudgePromptDocument todos)

let loopNudgePromptDocument (todos: string list) : PromptDocument =
    let todoTargets = todos |> List.map PromptTarget.TodoTarget

    let view =
        { objective = "Continue execution in review loop mode"
          background = Some "Review mode is active"
          agentRole = AgentRole.NudgeSupervisor
          targets = todoTargets
          boundaries = []
          rules =
            [ PromptRule.Contract
                  "Call submit_review with a detailed report and the complete affected-file list before finishing." ]
          outcomes =
            [ { label = "continue"
                text = "Submit review or complete remaining loop work." } ] }

    match PromptDocument.create view with
    | Ok doc -> doc
    | Error errs -> failwithf "Invalid loop nudge PromptDocument: %A" errs

let loopNudgePromptFor (todos: string list) : string =
    PromptToml.render (loopNudgePromptDocument todos)

let todoNudgePrompt = todoNudgePromptFor []
let loopNudgePrompt = loopNudgePromptFor []

let runnerNudgePromptDocument (_host: Host) : PromptDocument =
    let view =
        { objective = "Manage active background runner task"
          background = Some "A background runner task is still active"
          agentRole = AgentRole.NudgeSupervisor
          targets = []
          boundaries = []
          rules =
            [ PromptRule.Policy "Call runner_wait to collect output or runner_abort to stop it before finishing." ]
          outcomes =
            [ { label = "continue"
                text = "Resolve active runner task before ending session." } ] }

    match PromptDocument.create view with
    | Ok doc -> doc
    | Error errs -> failwithf "Invalid runner nudge PromptDocument: %A" errs

let runnerNudgePromptFor (host: Host) =
    PromptToml.render (runnerNudgePromptDocument host)

let runnerNudgePrompt = runnerNudgePromptFor opencode

/// Manager primary-agent system prompt as typed PromptDocument (not free prose module).
let managerSystemPromptDocument (_host: Host) : PromptDocument =
    let view =
        { objective = "Coordinate the overall task toward the user's original goal."
          background = None
          agentRole = AgentRole.NudgeSupervisor
          targets = []
          boundaries = []
          rules =
            [ PromptRule.Policy "Delegate specialized work to subagents when appropriate."
              PromptRule.Policy "Prefer complete, correct outcomes over premature finish." ]
          outcomes =
            [ { label = "coordinate"
                text = "Drive the session until the user's goal is satisfied." } ] }

    match PromptDocument.create view with
    | Ok doc -> doc
    | Error errs -> failwithf "Invalid manager system PromptDocument: %A" errs

let managerSystemPromptFor (host: Host) =
    PromptToml.render (managerSystemPromptDocument host)

let managerSystemPrompt = managerSystemPromptFor opencode

/// Parallel-tool orchestration hint as typed PromptDocument.
let parallelToolHintDocument: PromptDocument =
    let view =
        { objective = "Issue independent tool calls in one turn when possible."
          background = None
          agentRole = AgentRole.NudgeSupervisor
          targets = []
          boundaries = []
          rules =
            [ PromptRule.Policy "Batch independent read/grep/search/executor calls in a single assistant turn."
              PromptRule.Policy "Serialize only when a later call strictly depends on the prior result." ]
          outcomes =
            [ { label = "parallel_tools"
                text = "Prefer concurrent tool batches over serial independent steps." } ] }

    match PromptDocument.create view with
    | Ok doc -> doc
    | Error errs -> failwithf "Invalid parallel tool PromptDocument: %A" errs

let parallelToolHint = PromptToml.render parallelToolHintDocument
