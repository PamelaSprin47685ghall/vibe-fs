module VibeFs.Opencode.MagicCore

open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Message

let magicTodoToolNameFor (host: Host) : string = todoWriteToolName host
let magicTodoToolName = magicTodoToolNameFor opencode
let magicReviewToolName = "submit_review"

type BacklogEntry =
    { sequence: int
      timestamp: string
      report: string }

let isTodoResultFor (host: Host) (part: obj) : bool =
    partIsTool part
    && partToolName part = magicTodoToolNameFor host
    && partToolStatus part = "completed"

let isTodoResult (part: obj) : bool =
    isTodoResultFor opencode part

let isTodoErrorFor (host: Host) (part: obj) : bool =
    partIsTool part
    && partToolName part = magicTodoToolNameFor host
    && partToolStatus part = "error"

let isTodoError (part: obj) : bool =
    isTodoErrorFor opencode part

let lastTodoErrorTextFor (host: Host) (flat: FlatPart list) : string option =
    let mutable last = None
    for fp in flat do
        if isTodoErrorFor host fp.part then
            last <- Some(partToolError fp.part)
    last

let isReviewTool (part: obj) : bool =
    partIsTool part && partToolName part = magicReviewToolName

let emptyBacklogText = "[当前还没有已完成工作报告]"
let userMsgHeader = "[工作期间收到的用户消息]"
let foldHeader = "[已完成并折叠的工作记录] 以下报告来自被折叠的旧轮次，其中提到的文件修改已写入磁盘"
let sectionSep = "\n\n---\n\n"
let lineSep = "\n\n"
let dotSep = " . "
let errorPrefix = "[上次操作失败] "
let magicTodoProjectionPrefix = "magic-todo-projection-"
let magicTodoPrefixPrefix = "magic-todo-prefix-"

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    if backlog.IsEmpty && userPrompts.IsEmpty then
        emptyBacklogText
    else
        let parts = ResizeArray<string>()
        if userPrompts.Length > 0 then
            let joined = userPrompts |> List.mapi (fun index text -> string (index + 1) + ". " + text.Trim()) |> String.concat lineSep
            parts.Add(userMsgHeader + "\n" + joined)
        if not backlog.IsEmpty then
            let reports =
                backlog
                |> List.map (fun entry ->
                    let ts = if entry.timestamp <> "" then dotSep + entry.timestamp else ""
                    "#" + string entry.sequence + ts + "\n" + entry.report)
            parts.Add(foldHeader + "\n" + String.concat sectionSep reports)
        String.concat sectionSep parts

let lastTodoErrorText (flat: FlatPart list) : string option =
    lastTodoErrorTextFor opencode flat
