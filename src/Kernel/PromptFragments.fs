module Wanxiangshu.Kernel.PromptFragments

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.PromptFrontMatter

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

let todoNudgePromptProse =
    "There are still incomplete todos. Continue working through the remaining items. "
    + "If they are irrelevant, remove them. "
    + "If you want to skip this check, respond with <skip-todo-check />"

let loopNudgePromptProse =
    "You are in loop mode. You must call the submit_review tool to\n"
    + "submit your detailed report and list of modified files for review\n"
    + "before finishing. Do not end the conversation without calling submit_review."

let todoNudgePromptFor (todos: string list) : string =
    let fields = [ yamlStringSeqField "todos" todos ]
    frontMatterPrompt fields todoNudgePromptProse

let loopNudgePromptFor (todos: string list) : string =
    let fields =
        if List.isEmpty todos then
            []
        else
            [ yamlStringSeqField "todos" todos ]

    frontMatterPrompt fields loopNudgePromptProse

let todoNudgePrompt = todoNudgePromptProse
let loopNudgePrompt = loopNudgePromptProse

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

let parallelToolPromptProse =
    "【万象铁律】检测到上一轮仅执行了单工具调用。严禁单步调试式控制流，杜绝拖延。请穷尽当前可并行执行的步骤，一次性调用所有正交工具（如并行 fuzzy_grep/read/write/executor），严禁一次只调用一个工具！编译器和运行时已为你站岗，速去并行执行，提高效率！"
