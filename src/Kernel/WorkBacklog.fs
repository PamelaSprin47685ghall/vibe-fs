module Wanxiangshu.Kernel.WorkBacklog

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore

let private toolDescriptionHeader =
    "Manage a structured todo list and preserve a compact append-only work backlog. "
    + "Use this tool for multi-step coding work, appending a detailed completed-work handover report after each meaningful progress "
    + "to keep the user informed.\n\n"
    + "Critical write rules:\n"
    + "- Every call MUST include all five report fields: ahaMoments, changesAndReasons, gotchas, lessonsAndConventions, plan.\n"
    + "- Each field MUST be at least 1024 characters. Use high-density modern Chinese.\n"
    + "- ahaMoments: breakthroughs and key realizations discovered during this work step.\n"
    + "- changesAndReasons: what files or logic changed and the reasoning behind each change.\n"
    + "- gotchas: traps, edge cases, and surprises encountered.\n"
    + "- lessonsAndConventions: patterns, conventions, or lessons future developers should keep.\n"
    + "- plan: for initial planning, attach the detailed plan and explicitly state no implementation has started; for ongoing work, describe the next steps."

let private toolDescriptionTail =
    "Your session may be paused now and then and resumed later by other developers, so these five fields are the only way to hand over your work. "

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

let ahaMomentsDesc = "Required (min 1024 chars). Breakthroughs and key realizations discovered during this work step. Use high-density modern Chinese."
let changesAndReasonsDesc = "Required (min 1024 chars). What files or logic changed and the reasoning behind each change. Use high-density modern Chinese."
let gotchasDesc = "Required (min 1024 chars). Traps, edge cases, and surprises encountered. Use high-density modern Chinese."
let lessonsAndConventionsDesc = "Required (min 1024 chars). Patterns, conventions, or lessons future developers should keep. Use high-density modern Chinese."
let planDesc = "Required (min 1024 chars). For initial planning, attach the detailed plan and explicitly state no implementation has started; for ongoing work, describe the next steps. Use high-density modern Chinese."

let reportFieldNames = ["ahaMoments"; "changesAndReasons"; "gotchas"; "lessonsAndConventions"; "plan"]

let private consEntry (revAcc: BacklogEntry list) (entry: BacklogEntry) : BacklogEntry list =
    entry :: revAcc

/// Replay the message stream into a backlog. `entryOf` extracts the
/// BacklogEntry for a given flat tool-result part (host-specific Dyn
/// reading is injected by the caller, keeping this function pure).
let replayBacklogWith (host: Host) (entryOf: FlatPart<'raw> -> BacklogEntry option) (messages: Message<'raw> list) : BacklogEntry list =
    if messages.IsEmpty then
        []
    else
        let flat = flatten messages

        let revAcc =
            flat
            |> List.fold
                (fun acc fp ->
                    if isTodoResultFor host fp.part then
                        match entryOf fp with
                        | Some entry -> consEntry acc entry
                        | None -> acc
                    else
                        acc)
                []

        List.rev revAcc
