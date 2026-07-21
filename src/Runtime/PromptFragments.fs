module Wanxiangshu.Runtime.PromptFragments

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Nudge.NudgePromptText
open Wanxiangshu.Runtime.PromptHeader

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
    $"A background runner task is still active. Call runner_wait to collect output or runner_abort to stop it before finishing."

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
