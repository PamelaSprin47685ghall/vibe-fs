module Wanxiangshu.Kernel.WorkBacklog

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore

let private toolDescriptionHeader =
    "Manage a structured todo list and preserve a compact append-only work backlog. "
    + "Use this tool for multi-step coding work, appending a detailed completed-work handover report after each meaningful progress "
    + "to keep the user informed.\n\n"
    + "Critical write rules:\n"
    + "- Every call MUST include completedWorkReport.\n"
    + "- completedWorkReport is important and it must capture all aha moments, what changed and why, gotchas discovered, and lessons or conventions future developers should keep."

let private toolDescriptionTail =
    "Your session may be paused now and then and resumed later by other developers, so the completedWorkReport is the only way to hand over your work. "

let toolDescriptionFor (host: Host) =
    toolDescriptionHeader
    + "\n- Always provide the full todos list. Partial updates are not supported.\n\n"
    + toolDescriptionTail
    + "\n\n"
    + Wanxiangshu.Kernel.Methodology.selectMethodologyFieldDescription

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
    "Required. A detailed handover report of the work just completed before this todo update. Use high-density modern Chinese. "
    + "For initial planning, explicitly say that no implementation work has completed yet and attach the detailed plan. (effectively replace the old plan tool)"

let mimoReportFieldDesc = reportDesc

let private consEntry (revAcc: BacklogEntry list) (report: string) : BacklogEntry list =
    { report = report }
    :: revAcc

/// Replay the message stream into a backlog. `reportOf` extracts the completed-
/// work report string for a given flat tool-result part (host-specific Dyn
/// reading is injected by the caller, keeping this function pure).
let replayBacklogWith (host: Host) (reportOf: FlatPart<'raw> -> string) (messages: Message<'raw> list) : BacklogEntry list =
    if messages.IsEmpty then
        []
    else
        let flat = flatten messages

        let revAcc =
            flat
            |> List.fold
                (fun acc fp ->
                    if isTodoResultFor host fp.part then
                        let report = reportOf fp
                        if report <> "" then consEntry acc report else acc
                    else
                        acc)
                []

        List.rev revAcc
