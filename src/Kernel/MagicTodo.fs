module VibeFs.Kernel.MagicTodo

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Message
open VibeFs.Kernel.Dyn
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
    match host with
    | Opencode ->
        toolDescriptionHeader + "\n- Always provide the full todos list. Partial updates are not supported.\n\n"
        + toolDescriptionTail
    | Mimocode ->
        toolDescriptionHeader + "\n\n" + toolDescriptionTail

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

let mimoReportFieldDesc =
    reportDesc
    + " CRITICAL: place `completedWorkReport` as a TOP-LEVEL argument, a sibling of `operation`. "
    + "Never nest it inside the `operation` object."

let backlogReportFromTodoInput (host: Host) (input: obj) : string =
    let explicit = str input "completedWorkReport"
    if explicit.Trim() <> "" then explicit.Trim()
    elif host = Mimocode then
        let operation = get input "operation"
        if isNullish operation then ""
        else
            let eventSummary = str operation "event_summary"
            if eventSummary.Trim() <> "" then eventSummary.Trim()
            else
                match str operation "action" with
                | "create" ->
                    let summary = str operation "summary"
                    if summary.Trim() = "" then "" else "Created task: " + summary.Trim()
                | _ -> ""
    else ""

let private consEntry (revAcc: BacklogEntry list) (report: string) : BacklogEntry list =
    { sequence = revAcc.Length + 1; timestamp = ""; report = report } :: revAcc

let private flushBurst (revAcc: BacklogEntry list) (revBurst: string list) : BacklogEntry list =
    match revBurst with
    | [] -> revAcc
    | _ ->
        let burst = List.rev revBurst
        let merged =
            match burst with
            | [ single ] -> single
            | _ -> burst |> List.mapi (fun i line -> string (i + 1) + ". " + line) |> String.concat "\n"
        consEntry revAcc merged

let replayBacklogWith (host: Host) (inputForPart: FlatPart -> obj) (messages: obj array) : BacklogEntry list =
    if isNullish messages then []
    else
        let flat = flatten messages
        match host with
        | Opencode ->
            let revAcc =
                flat
                |> List.fold
                    (fun acc fp ->
                        if isTodoResultFor host fp.part then
                            let input = inputForPart fp
                            if isNullish input then acc
                            else
                                let report = backlogReportFromTodoInput host input
                                if report <> "" then consEntry acc report else acc
                        else acc)
                    []
            List.rev revAcc
        | Mimocode ->
            let revAcc, revBurst =
                flat
                |> List.fold
                    (fun (revAcc, revBurst) fp ->
                        if isTodoResultFor host fp.part then
                            let input = inputForPart fp
                            if isNullish input then (revAcc, revBurst)
                            else
                                let report = backlogReportFromTodoInput host input
                                if report <> "" then (revAcc, report :: revBurst) else (revAcc, revBurst)
                        elif breaksTodoBurstFor host fp then
                            let revAcc' = flushBurst revAcc revBurst
                            (revAcc', [])
                        else
                            (revAcc, revBurst))
                    ([], [])
            flushBurst revAcc revBurst |> List.rev