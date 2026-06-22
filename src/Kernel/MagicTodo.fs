module VibeFs.Kernel.MagicTodo

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore

let private toolDescriptionHeader =
    "Manage a structured todo list and preserve a compact append-only work backlog. "
    + "Use this tool for multi-step coding work, appending a detailed completed-work report "
    + "to keep future context folding informed.\n\n"
    + "Critical write rules:\n"
    + "- Every call MUST include completedWorkReport.\n"
    + "- completedWorkReport is stored forever in Magic Todo's append-only backlog. It must capture what changed and why, key files read or written (full paths), gotchas discovered, and lessons or conventions future developers should keep.\n"
    + "- Do not batch many completed tasks into one vague report; write after meaningful progress."

let private toolDescriptionTail =
    "Context folding behavior:\n"
    + "- Later turns may see backlog projections instead of older raw todo updates.\n"
    + "- Detailed context between older todo writes can be folded away, so completedWorkReport must preserve anything future turns need."

let toolDescriptionFor (host: Host) =
    toolDescriptionHeader + "\n- Always provide the full todos list. Partial updates are not supported.\n\n"
    + toolDescriptionTail

let toolDescription = toolDescriptionFor opencode

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
    + "Verbosity is encouraged - this report is preserved in an append-only backlog that "
    + "survives context folding, so it must contain everything future turns need."

let mimoReportFieldDesc = reportDesc

let private consEntry (revAcc: BacklogEntry list) (report: string) : BacklogEntry list =
    { sequence = revAcc.Length + 1; timestamp = ""; report = report } :: revAcc

/// Replay the message stream into a backlog. `reportOf` extracts the completed-
/// work report string for a given flat tool-result part (host-specific Dyn
/// reading is injected by the caller, keeping this function pure).
let replayBacklogWith (host: Host) (reportOf: FlatPart -> string) (messages: Message list) : BacklogEntry list =
    if messages.IsEmpty then []
    else
        let flat = flatten messages
        let revAcc =
            flat
            |> List.fold
                (fun acc fp ->
                    if isTodoResultFor host fp.part then
                        let report = reportOf fp
                        if report <> "" then consEntry acc report else acc
                    else acc)
                []
        List.rev revAcc
