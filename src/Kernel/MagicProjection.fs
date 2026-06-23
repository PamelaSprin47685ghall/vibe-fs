module VibeFs.Kernel.MagicProjection

open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.MagicCore

let private isFoldAnchorFor (host: Host) (part: Part) : bool =
    isTodoResultFor host part

type FoldRange = { firstResult: int; secondToLast: int }

let private todoIndexesFor (host: Host) (flat: FlatPart list) : int list =
    flat
    |> List.indexed
    |> List.choose (fun (index, fp) -> if isFoldAnchorFor host fp.part then Some index else None)

let private todoIndexes (flat: FlatPart list) : int list =
    todoIndexesFor opencode flat

let private todoSegmentEndIndexesFor (host: Host) (flat: FlatPart list) : int list =
    todoIndexesFor host flat

let private foldTodoAnchorsFor (host: Host) (flat: FlatPart list) : int list =
    todoSegmentEndIndexesFor host flat

let private requiredFoldAnchorCount (foldAfterFirst: bool) : int =
    if foldAfterFirst then 2 else 3

let private messageTimeOrNull (msg: Message) : obj = msg.info.time

let private collectUserText (flat: FlatPart list) (fromIdx: int) (toIdx: int) : string list =
    let lo = max 0 fromIdx
    let hi = min (flat.Length - 1) toIdx
    if lo > hi then []
    else
        flat.[lo..hi]
        |> List.choose (fun fp ->
            if fp.isUser && partIsText fp.part then
                let t = (partTextStr fp.part).Trim()
                if t <> "" then Some t else None
            else
                None)

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

let private buildPrefixUserMessage (id: string) (text: string) (sessionID: string) (time: obj) : Message =
    { info =
          { id = id
            sessionID = sessionID
            role = User
            agent = "orchestrator"
            isError = false
            toolName = ""
            details = null
            time = time }
      parts = [ TextPart text ]
      source = Native
      raw = null }

let private buildSyntheticPrefixMessages (host: Host) (messages: Message list) (flat: FlatPart list) (foldedBacklog: BacklogEntry list) (sessionID: string) (errorNotice: string option) : Message list =
    let todoIdxs = foldTodoAnchorsFor host flat
    [ for index in 0 .. foldedBacklog.Length - 1 do
          let fromIdx = if index = 0 then 0 else todoIdxs.[index - 1] + 1
          let toIdx = todoIdxs.[index] - 1
          let userText = collectUserText flat fromIdx toIdx
          let finalText =
              if index = foldedBacklog.Length - 1 then
                  buildBacklogTextWithError [ foldedBacklog.[index] ] userText errorNotice
              else
                  buildBacklogText [ foldedBacklog.[index] ] userText
          let todoMessage = messages.[flat.[todoIdxs.[index]].msgIndex]
          let todoTime = messageTimeOrNull todoMessage
          let syntheticId = magicTodoPrefixPrefix + string (index + 1)
          yield buildPrefixUserMessage syntheticId finalText todoMessage.info.sessionID todoTime ]

let private rebuildVisibleOnly (messages: Message list) (visible: FlatPart list) : Message list =
    let byMessage = visible |> List.groupBy (fun entry -> entry.msgIndex) |> Map.ofList
    messages
    |> List.indexed
    |> List.choose (fun (msgIdx, msg) ->
        match Map.tryFind msgIdx byMessage with
        | None -> None
        | Some entries ->
            let partMap = entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList
            let newParts =
                msg.parts
                |> List.indexed
                |> List.choose (fun (partIdx, part) -> Map.tryFind partIdx partMap)
            if newParts.IsEmpty then None else Some { msg with parts = newParts })

let projectMagicFor (host: Host) (messages: Message list) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : Message list =
    if messages.IsEmpty then messages
    else
        let flat = flatten messages
        match findFoldRangeFor host flat foldAfterFirst with
        | None -> messages
        | Some range ->
            let foldedBacklog = if backlog.Length > 0 then backlog.[.. backlog.Length - 2] else []
            let middleUserText = collectUserText flat (range.firstResult + 1) (range.secondToLast - 1)
            let projectionText = buildBacklogText foldedBacklog middleUserText
            let projectionPart = setPartOutputTyped flat.[range.firstResult].part projectionText
            let errorNotice = lastTodoErrorTextFor host flat
            let syntheticPrefixMessages = if foldedBacklog.IsEmpty then [] else buildSyntheticPrefixMessages host messages flat foldedBacklog sessionID errorNotice
            let visible =
                flat
                |> List.indexed
                |> List.choose (fun (i, fp) ->
                    if i < range.firstResult then None
                    elif i = range.firstResult then Some { fp with part = projectionPart }
                    elif i < range.secondToLast then if isReviewTool fp.part then Some fp else None
                    elif isTodoErrorFor host fp.part then None
                    else Some fp)
            let rebuilt = rebuildVisibleOnly messages visible
            if syntheticPrefixMessages.IsEmpty then rebuilt else syntheticPrefixMessages @ rebuilt

let projectMagic (messages: Message list) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : Message list =
    projectMagicFor opencode messages backlog foldAfterFirst sessionID
