module VibeFs.Opencode.MagicPrompts

let toolDescription =
    "Manage a structured todo list and preserve a compact append-only work backlog. "
    + "Use this tool for multi-step coding work. Every call replaces the entire todo list, appends a detailed completed-work report, "
    + "and keeps future context folding informed.\n\n"
    + "Critical write rules:\n"
    + "- Every call MUST include completedWorkReport.\n"
    + "- completedWorkReport is stored forever in Magic Todo's append-only backlog. It must capture what changed and why, key files read or written (full paths), gotchas discovered, and lessons or conventions future developers should keep.\n"
    + "- Do not batch many completed tasks into one vague report; write after meaningful progress.\n"
    + "- Always provide the full todos list. Partial updates are not supported.\n\n"
    + "Context folding behavior:\n"
    + "- Later turns may see backlog projections instead of older raw todo updates.\n"
    + "- Detailed context between older todo writes can be folded away, so completedWorkReport must preserve anything future turns need."

let todosDesc =
    "Complete replacement todo list. Re-send every remaining item, not just the ones that changed, and keep exactly one in_progress item while active work remains whenever possible."

let todoContentDesc =
    "Brief action-oriented task description. Include the concrete next step, relevant paths, or acceptance criteria when useful."

let todoStatusDesc =
    "Current status of the task: pending, in_progress, completed, cancelled. Keep exactly one in_progress item while work remains whenever possible."

let todoPriorityDesc =
    "Priority level of the task: high, medium, low. Use priority to show execution order, not implementation difficulty."

let reportDesc =
    "Required. A detailed report of the work just completed before this todo update. "
    + "Must include: 1) what work was done and why, 2) key files read or written (full paths), "
    + "3) any gotchas or non-obvious issues discovered, 4) lessons learned for future developers. "
    + "For initial planning, explicitly say that no implementation work has completed yet and summarize the planning change. "
    + "Verbosity is encouraged — this report is preserved in an append-only backlog that "
    + "survives context folding, so it must contain everything future turns need."
