module VibeFs.Kernel.MagicCore

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging

let magicTodoToolNameFor (host: Host) : string = todoWriteToolName host
let magicTodoToolName = magicTodoToolNameFor opencode
let magicReviewToolName = "submit_review"

type BacklogEntry =
    { sequence: int
      timestamp: string
      report: string }

let isTodoResultFor (host: Host) (part: Part) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = magicTodoToolNameFor host && state.status = "completed" -> true
    | _ -> false

let isTodoResult (part: Part) : bool =
    isTodoResultFor opencode part

let isTodoErrorFor (host: Host) (part: Part) : bool =
    match part with
    | ToolPart(toolName, _, Some state, _) when toolName = magicTodoToolNameFor host && state.status = "error" -> true
    | _ -> false

let isTodoError (part: Part) : bool =
    isTodoErrorFor opencode part

let lastTodoErrorTextFor (host: Host) (flat: FlatPart list) : string option =
    flat
    |> List.tryFindBack (fun fp -> isTodoErrorFor host fp.part)
    |> Option.map (fun fp ->
        match fp.part with
        | ToolPart(_, _, Some state, _) -> state.error
        | _ -> "")

/// Mimocode 连续 task 调用 burst 的打断判定：用户消息（插话/输入）或其他工具调用会打断；
/// assistant 文本输出、reasoning 思考、进度 part 不打断。
let breaksTodoBurstFor (host: Host) (fp: FlatPart) : bool =
    fp.isUser
    || (match fp.part with
        | ToolPart(toolName, _, _, _) when toolName <> magicTodoToolNameFor host -> true
        | _ -> false)

let isReviewTool (part: Part) : bool =
    match part with
    | ToolPart(toolName, _, _, _) when toolName = magicReviewToolName -> true
    | _ -> false

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
