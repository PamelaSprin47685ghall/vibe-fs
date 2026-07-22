module Wanxiangshu.Runtime.PromptFragments

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge.NudgePromptText
open Wanxiangshu.Kernel.Prompt
open Wanxiangshu.Runtime.Prompt

let todoNudgePromptProse =
    Wanxiangshu.Kernel.Nudge.NudgePromptText.todoNudgePromptProse

let loopNudgePromptProse =
    Wanxiangshu.Kernel.Nudge.NudgePromptText.loopNudgePromptProse

let readOnlyRulesFor (host: Host) =
    "READ-ONLY: You must NOT write, edit, patch, or create files. "
    + "You must NOT run commands or call "
    + todoWritePromptName host
    + " or any mutating tool. "
    + "You must NOT change workspace state. Output detailed reports only."

let readOnlyRules = readOnlyRulesFor opencode

let readOnlyWorkspaceConstraint = readOnlyRules

let reviewCriteria =
    """# Evaluation Criteria

1. Does the implementation make full use of language features? Are the correct algorithms and data structures used?
2. Is the implementation no more complex than necessary? Are there any garbage code, dead code, legacy compatible wrappers or unnecessary workarounds?
3. Is the program structure elegant and free of redundancy?
4. Are there no oversized files, overly long functions, or avoidable complexity?
5. Are there necessary unit or integration tests?
6. Are there design flaws, logic errors, or best-practice violations?
7. Is the result natural and intuitive for the user or caller?
8. Does it fully satisfy the original task without cutting corners?"""

let todoNudgePromptDocument (todos: string list) : PromptDocument =
    let targets = todos |> List.map PromptTarget.TodoTarget

    let view =
        { objective = "Continue incomplete work and finish the next pending todo"
          background = Some "The stream ended while work remained open"
          agentRole = AgentRole.NudgeSupervisor
          targets = targets
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
    let targets = todos |> List.map PromptTarget.TodoTarget

    let view =
        { objective = "Continue execution in review loop mode"
          background = Some "You are in loop mode. You must call the submit_review tool before finishing."
          agentRole = AgentRole.NudgeSupervisor
          targets = targets
          boundaries = []
          rules =
            [ PromptRule.Policy
                  "Call the submit_review tool to submit your detailed report and list of modified files for review." ]
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

let runnerNudgePromptDocument (host: Host) : PromptDocument =
    let view =
        { objective = "Manage active background runner task"
          background = Some "A background runner task is still active."
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

let managerSystemPromptFor (host: Host) =
    "You are the manager agent. Coordinate the overall task, work towards the user's original goal."

let managerSystemPrompt = managerSystemPromptFor opencode

let parallelToolPromptProse =
    "Hint: if your next response can perform several independent tool calls "
    + "(for example multiple `read`/`fuzzy_grep`/`executor`/`bash` operations on "
    + "different targets, or a mix of independent reads, greps and searches), "
    + "issue them all in one assistant turn. The runtime executes parallel "
    + "tool calls concurrently and there is no reason to serialize independent "
    + "operations. Reserve a single tool call only when the next step strictly "
    + "depends on the current result."
