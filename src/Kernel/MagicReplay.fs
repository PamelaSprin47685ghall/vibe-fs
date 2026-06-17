module VibeFs.Kernel.MagicReplay

open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MagicTypes
open VibeFs.Kernel.PartStream

let private isCompletedTodo (part: obj) : bool =
    partIsTool part && partToolName part = magicTodoToolName && partToolStatus part = "completed"

let replayBacklog (messages: obj array) : BacklogEntry list =
    if isNullish messages then []
    else
        let flat = flatten messages
        let backlog = ResizeArray<BacklogEntry>()
        for fp in flat do
            if isCompletedTodo fp.part then
                let input = partToolInput fp.part
                if not (isNullish input) then
                    let report = str input "completedWorkReport"
                    if report.Trim() <> "" then
                        backlog.Add({ sequence = backlog.Count + 1; timestamp = ""; report = report.Trim() })
        List.ofSeq backlog
