module VibeFs.Kernel.MagicCore

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
    flat
    |> List.tryFindBack (fun fp -> isTodoErrorFor host fp.part)
    |> Option.map (fun fp -> partToolError fp.part)

/// Mimocode 连续 task 调用 burst 的打断判定：用户消息（插话/输入）或其他工具调用会打断；
/// assistant 文本输出、reasoning 思考、进度 part 不打断。
let breaksTodoBurstFor (host: Host) (fp: FlatPart) : bool =
    fp.isUser || (partIsTool fp.part && partToolName fp.part <> magicTodoToolNameFor host)

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
        let userBlock =
            if userPrompts.IsEmpty then
                []
            else
                let joined =
                    userPrompts
                    |> List.mapi (fun index text -> string (index + 1) + ". " + text.Trim())
                    |> String.concat lineSep
                [ userMsgHeader + "\n" + joined ]
        let backlogBlock =
            if backlog.IsEmpty then
                []
            else
                let reports =
                    backlog
                    |> List.map (fun entry ->
                        let ts = if entry.timestamp <> "" then dotSep + entry.timestamp else ""
                        "#" + string entry.sequence + ts + "\n" + entry.report)
                [ foldHeader + "\n" + String.concat sectionSep reports ]
        userBlock @ backlogBlock |> String.concat sectionSep

let lastTodoErrorText (flat: FlatPart list) : string option =
    lastTodoErrorTextFor opencode flat