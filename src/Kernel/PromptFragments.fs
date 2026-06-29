module Wanxiangshu.Kernel.PromptFragments

open Wanxiangshu.Kernel.HostTools

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

let todoNudgePrompt =
    "There are still incomplete todos. Continue working through the remaining items. "
    + "If they are irrelevant, remove them. "
    + "If you want to skip this check, respond with <skip-todo-check />"

let loopNudgePrompt =
    "You are in loop mode. You must call the submit_review tool to\n"
    + "submit your detailed report and list of modified files for review\n"
    + "before finishing. Do not end the conversation without calling submit_review."

let runnerNudgePromptFor (host: Host) =
    let waitTool, abortTool =
        match host with
        | Omp -> "executor_wait", "executor_abort"
        | _ -> "runner_wait", "runner_abort"
    $"A background runner task is still active. Call {waitTool} to collect output or {abortTool} to stop it before finishing."

let runnerNudgePrompt = runnerNudgePromptFor opencode

let managerSystemPromptFor (host: Host) =
    let todoLine =
        "For multi-step work, keep "
        + todoWriteToolName host
        + " current. Every "
        + todoWriteToolName host
        + " call must provide the full todos list plus five detailed report fields (ahaMoments, changesAndReasons, gotchas, lessonsAndConventions, plan), each at least 1024 characters, that can survive context folding."

    "You are the manager agent. Coordinate the overall task, work towards the user's original goal.\n\n"
    + todoLine

let managerSystemPrompt = managerSystemPromptFor opencode
