module VibeFs.Opencode.MagicProjection

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Opencode.MagicCore

let private isReadOnlyMimocodeTaskResult (part: obj) : bool =
    if not (isTodoResultFor Mimocode part) then false
    else
        let state = get part "state"
        let input = if isNullish state then null else get state "input"
        let operation = if isNullish input then null else get input "operation"
        match (if isNullish operation then "" else str operation "action") with
        | "list"
        | "get" -> true
        | _ -> false

let private isFoldAnchorFor (host: Host) (part: obj) : bool =
    isTodoResultFor host part
    && (host <> Mimocode || not (isReadOnlyMimocodeTaskResult part))

type FoldRange = { firstResult: int; secondToLast: int }

let private todoIndexesFor (host: Host) (flat: FlatPart list) : int list =
    flat
    |> List.indexed
    |> List.choose (fun (index, fp) -> if isFoldAnchorFor host fp.part then Some index else None)

let private todoIndexes (flat: FlatPart list) : int list =
    todoIndexesFor opencode flat

let private todoSegmentEndIndexesFor (host: Host) (flat: FlatPart list) : int list =
    match host with
    | Opencode -> todoIndexesFor host flat
    | Mimocode ->
        if flat.IsEmpty then []
        else
            let ends = ResizeArray<int>()
            let mutable inBurst = false
            let mutable lastAnchor = -1
            for i = 0 to flat.Length - 1 do
                if isTodoResultFor host flat.[i].part then
                    inBurst <- true
                    if isFoldAnchorFor host flat.[i].part then lastAnchor <- i
                elif inBurst then
                    if lastAnchor >= 0 then ends.Add(lastAnchor)
                    inBurst <- false
                    lastAnchor <- -1
            if inBurst && lastAnchor >= 0 then ends.Add(lastAnchor)
            List.ofSeq ends

let private foldTodoAnchorsFor (host: Host) (flat: FlatPart list) : int list =
    todoSegmentEndIndexesFor host flat

let private requiredFoldAnchorCount (foldAfterFirst: bool) : int =
    if foldAfterFirst then 2 else 3

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
                if text.Trim() <> "" then result.Add(text.Trim())
    List.ofSeq result

let findFoldRangeFor (host: Host) (flat: FlatPart list) (foldAfterFirst: bool) : FoldRange option =
    let todoIdxs = foldTodoAnchorsFor host flat
    let minResults = requiredFoldAnchorCount foldAfterFirst
    if todoIdxs.Length < minResults then None
    else
        let firstResult = todoIdxs.[0]
        let secondToLast = todoIdxs.[todoIdxs.Length - 2]
        if secondToLast <= firstResult then None else Some { firstResult = firstResult; secondToLast = secondToLast }

let findFoldRange (flat: FlatPart list) (foldAfterFirst: bool) : FoldRange option =
    findFoldRangeFor opencode flat foldAfterFirst

let private buildPrefixUserMessage (id: string) (text: string) (sessionID: string) (time: obj) : obj =
    let messageTime = if isNullish time then box (createObj [ "created", box 0 ]) else time
    let info = createObj [ "id", box id; "sessionID", box sessionID; "role", box "user"; "time", messageTime; "agent", box "orchestrator"; "model", box (createObj [ "providerID", box ""; "modelID", box "" ]) ]
    box (createObj [ "info", box info; "parts", box [| box {| ``type`` = "text"; text = text |} |] ])

let private buildSyntheticPrefixMessages (host: Host) (messages: obj array) (flat: FlatPart list) (foldedBacklog: BacklogEntry list) (sessionID: string) (errorNotice: string option) : obj array =
    let todoIdxs = foldTodoAnchorsFor host flat
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
        let syntheticId = magicTodoPrefixPrefix + string (index + 1)
        result.Add(buildPrefixUserMessage syntheticId finalText (if isNullish todoInfo then sessionID else infoSessionID todoInfo) todoTime)
    result.ToArray()

let private rebuildVisibleOnly (messages: obj array) (visible: FlatPart list) : obj array =
    let byMessage = visible |> List.groupBy (fun entry -> entry.msgIndex) |> Map.ofList
    let result = ResizeArray<obj>()
    for msgIdx = 0 to messages.Length - 1 do
        match Map.tryFind msgIdx byMessage with
        | None -> ()
        | Some entries ->
            let msg = messages.[msgIdx]
            if isNullish msg then ()
            else
                let originalParts = messageParts msg
                if isNullish originalParts || not (isArray originalParts) then
                    result.Add msg
                else
                    let partMap = entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList
                    let partsArr = originalParts :?> obj array
                    let newParts = ResizeArray<obj>()
                    for partIdx = 0 to partsArr.Length - 1 do
                        match Map.tryFind partIdx partMap with
                        | Some part -> newParts.Add part
                        | None -> ()
                    if newParts.Count > 0 then result.Add(withKey msg "parts" (box (newParts.ToArray())))
    result.ToArray()

let projectMagicFor (host: Host) (messages: obj array) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : obj array =
    if isNullish messages then [||]
    else
        let flat = flatten messages
        match findFoldRangeFor host flat foldAfterFirst with
        | None -> messages
        | Some range ->
            let foldedBacklog = if backlog.Length > 0 then backlog.[.. backlog.Length - 2] else []
            let middleUserText = collectUserText flat (range.firstResult + 1) (range.secondToLast - 1)
            let projectionText = buildBacklogText foldedBacklog middleUserText
            let projectionPart = setPartOutput flat.[range.firstResult].part projectionText
            let errorNotice = lastTodoErrorTextFor host flat
            let syntheticPrefixMessages = if foldedBacklog.IsEmpty then [||] else buildSyntheticPrefixMessages host messages flat foldedBacklog sessionID errorNotice
            let visible = ResizeArray<FlatPart>()
            for i = 0 to flat.Length - 1 do
                let fp = flat.[i]
                if i < range.firstResult then ()
                elif i = range.firstResult then visible.Add { fp with part = projectionPart }
                elif i < range.secondToLast then
                    if isReviewTool fp.part then
                        visible.Add fp
                elif isTodoErrorFor host fp.part then ()
                else visible.Add fp
            let rebuilt = rebuildVisibleOnly messages (List.ofSeq visible)
            if syntheticPrefixMessages.Length = 0 then rebuilt else Array.concat [| syntheticPrefixMessages; rebuilt |]

let projectMagic (messages: obj array) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : obj array =
    projectMagicFor opencode messages backlog foldAfterFirst sessionID
