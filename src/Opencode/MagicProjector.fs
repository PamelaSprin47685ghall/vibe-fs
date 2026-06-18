module VibeFs.Opencode.MagicProjector

open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDecoder
open VibeFs.Opencode.Magic
open VibeFs.Kernel.PartStream

let isTodoResult (part: obj) : bool =
    partIsTool part
    && partToolName part = magicTodoToolName
    && partToolStatus part = "completed"

let isTodoError (part: obj) : bool =
    partIsTool part
    && partToolName part = magicTodoToolName
    && partToolStatus part = "error"

let isReviewTool (part: obj) : bool =
    partIsTool part && partToolName part = magicReviewToolName

let private emptyBacklogText = "[当前还没有已完成工作报告]"
let private userMsgHeader = "[工作期间收到的用户消息]"
let private foldHeader = "[已完成并折叠的工作记录] 以下报告来自被折叠的旧轮次，其中提到的文件修改已写入磁盘"
let private sectionSep = "\n\n---\n\n"
let private lineSep = "\n\n"
let private dotSep = " . "
let private errorPrefix = "[上次操作失败] "

let private todoIndexes (flat: FlatPart list) : int list =
    flat
    |> List.indexed
    |> List.choose (fun (index, fp) -> if isTodoResult fp.part then Some index else None)

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    if backlog.IsEmpty && userPrompts.IsEmpty then
        emptyBacklogText
    else
        let parts = ResizeArray<string>()

        if userPrompts.Length > 0 then
            let joined =
                userPrompts
                |> List.mapi (fun index text -> string (index + 1) + ". " + text.Trim())
                |> String.concat lineSep

            parts.Add(userMsgHeader + "\n" + joined)

        if not backlog.IsEmpty then
            let reports =
                backlog
                |> List.map (fun entry ->
                    let ts =
                        if entry.timestamp <> "" then
                            dotSep + entry.timestamp
                        else
                            ""

                    "#" + string entry.sequence + ts + "\n" + entry.report)

            parts.Add(foldHeader + "\n" + String.concat sectionSep reports)

        String.concat sectionSep parts

let private lastTodoErrorText (flat: FlatPart list) : string option =
    let mutable last = None

    for fp in flat do
        if isTodoError fp.part then
            last <- Some(partToolError fp.part)

    last

let private messageTimeOrNull (msg: obj) : obj =
    let info = messageInfo msg
    if isNullish info then null else get info "time"

let private collectUserText (flat: FlatPart list) (fromIdx: int) (toIdx: int) : string list =
    let result = ResizeArray<string>()

    for i = fromIdx to toIdx do
        if i >= 0 && i < flat.Length then
            let fp = flat.[i]

            if fp.isUser && partIsText fp.part then
                let text = partTextStr fp.part

                if text.Trim() <> "" then
                    result.Add(text.Trim())

    List.ofSeq result

type FoldRange = { firstResult: int; secondToLast: int }

let findFoldRange (flat: FlatPart list) (foldAfterFirst: bool) : FoldRange option =
    let todoIdxs = todoIndexes flat

    let minResults = if foldAfterFirst then 2 else 3

    if todoIdxs.Length < minResults then
        None
    else
        let firstResult = todoIdxs.[0]
        let secondToLast = todoIdxs.[todoIdxs.Length - 2]

        if secondToLast <= firstResult then
            None
        else
            Some
                { firstResult = firstResult
                  secondToLast = secondToLast }

let private buildPrefixUserMessage (id: string) (text: string) (sessionID: string) (time: obj) : obj =
    let messageTime =
        if isNullish time then
            box (createObj [ "created", box 0 ])
        else
            time

    let info =
        createObj
            [ "id", box id
              "sessionID", box sessionID
              "role", box "user"
              "time", messageTime
              "agent", box "orchestrator"
              "model", box (createObj [ "providerID", box ""; "modelID", box "" ]) ]

    box (
        createObj [ "info", box info; "parts", box [| box {| ``type`` = "text"; text = text |} |] ]
    )

let private buildSyntheticPrefixMessages
    (messages: obj array)
    (flat: FlatPart list)
    (foldedBacklog: BacklogEntry list)
    (sessionID: string)
    (errorNotice: string option)
    : obj array =
    let todoIdxs = todoIndexes flat
    let result = ResizeArray<obj>()

    for index = 0 to foldedBacklog.Length - 1 do
        let fromIdx = if index = 0 then 0 else todoIdxs.[index - 1] + 1
        let toIdx = todoIdxs.[index] - 1
        let userText = collectUserText flat fromIdx toIdx
        let messageText = buildBacklogText [ foldedBacklog.[index] ] userText

        let finalText =
            if index = foldedBacklog.Length - 1 then
                match errorNotice with
                | Some err when err <> "" -> messageText + sectionSep + errorPrefix + err
                | _ -> messageText
            else
                messageText

        let todoMessage = messages.[flat.[todoIdxs.[index]].msgIndex]
        let todoInfo = messageInfo todoMessage
        let todoTime = messageTimeOrNull todoMessage
        let syntheticId =
            if index = 0 then magicTodoPrefixPrefix + string (index + 1)
            else magicTodoProjectionPrefix + string (index + 1)

        result.Add(
            buildPrefixUserMessage
                syntheticId
                finalText
                (if isNullish todoInfo then sessionID else infoSessionID todoInfo)
                todoTime
        )

    result.ToArray()

let private rebuildVisibleOnly (messages: obj array) (visible: FlatPart list) : obj array =
    let byMessage = visible |> List.groupBy (fun entry -> entry.msgIndex) |> Map.ofList
    let result = ResizeArray<obj>()

    for msgIdx = 0 to messages.Length - 1 do
        match Map.tryFind msgIdx byMessage with
        | None -> ()
        | Some entries ->
            let msg = messages.[msgIdx]

            if isNullish msg then
                ()
            else
                let originalParts = messageParts msg

                if isNullish originalParts || not (isArray originalParts) then
                    result.Add msg
                else
                    let partMap =
                        entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList

                    let partsArr = originalParts :?> obj array
                    let newParts = ResizeArray<obj>()

                    for partIdx = 0 to partsArr.Length - 1 do
                        match Map.tryFind partIdx partMap with
                        | Some part -> newParts.Add part
                        | None -> ()

                    if newParts.Count > 0 then
                        let cloneMsg = clone msg
                        cloneMsg?("parts") <- box (newParts.ToArray())
                        result.Add cloneMsg

    result.ToArray()

let projectMagic
    (messages: obj array)
    (backlog: BacklogEntry list)
    (foldAfterFirst: bool)
    (sessionID: string)
    : obj array =
    if isNullish messages then
        [||]
    else
        let flat = flatten messages

        match findFoldRange flat foldAfterFirst with
        | None -> messages
        | Some range ->
            let foldedBacklog =
                if backlog.Length > 0 then
                    backlog.[.. backlog.Length - 2]
                else
                    []

            let middleUserText =
                collectUserText flat (range.firstResult + 1) (range.secondToLast - 1)

            let projectionText = buildBacklogText foldedBacklog middleUserText
            let projectionPart = setPartOutput flat.[range.firstResult].part projectionText
            let errorNotice = lastTodoErrorText flat

            let syntheticPrefixMessages =
                if foldedBacklog.IsEmpty then
                    [||]
                else
                    buildSyntheticPrefixMessages messages flat foldedBacklog sessionID errorNotice

            let visible = ResizeArray<FlatPart>()

            for i = 0 to flat.Length - 1 do
                let fp = flat.[i]

                if i < range.firstResult then
                    ()
                elif i = range.firstResult then
                    visible.Add { fp with part = projectionPart }
                elif i < range.secondToLast then
                    if isReviewTool fp.part then visible.Add fp else ()
                elif isTodoError fp.part then
                    ()
                else
                    visible.Add fp

            let rebuilt = rebuildVisibleOnly messages (List.ofSeq visible)

            if syntheticPrefixMessages.Length = 0 then
                rebuilt
            else
                Array.concat [| syntheticPrefixMessages; rebuilt |]
