module VibeFs.Kernel.MagicProjection

open Fable.Core.JsInterop
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Kernel.MagicCore

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
            let inBurst, lastAnchor, endsRev =
                flat
                |> List.indexed
                |> List.fold
                    (fun (inBurst, lastAnchor, endsRev) (i, fp) ->
                        if isTodoResultFor host fp.part then
                            (true, (if isFoldAnchorFor host fp.part then i else lastAnchor), endsRev)
                        elif inBurst && breaksTodoBurstFor host fp then
                            (false, -1, (if lastAnchor >= 0 then lastAnchor :: endsRev else endsRev))
                        else
                            (inBurst, lastAnchor, endsRev))
                    (false, -1, [])
            let endsRev =
                if inBurst && lastAnchor >= 0 then lastAnchor :: endsRev else endsRev
            List.rev endsRev

let private foldTodoAnchorsFor (host: Host) (flat: FlatPart list) : int list =
    todoSegmentEndIndexesFor host flat

let private requiredFoldAnchorCount (foldAfterFirst: bool) : int =
    if foldAfterFirst then 2 else 3

let private messageTimeOrNull (msg: obj) : obj =
    let info = messageInfo msg
    if isNullish info then null else get info "time"

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

let private buildPrefixUserMessage (id: string) (text: string) (sessionID: string) (time: obj) : obj =
    let messageTime = if isNullish time then box (createObj [ "created", box 0 ]) else time
    let info = createObj [ "id", box id; "sessionID", box sessionID; "role", box "user"; "time", messageTime; "agent", box "orchestrator"; "model", box (createObj [ "providerID", box ""; "modelID", box "" ]) ]
    box (createObj [ "info", box info; "parts", box [| box {| ``type`` = "text"; text = text |} |] ])

let private buildSyntheticPrefixMessages (host: Host) (messages: obj array) (flat: FlatPart list) (foldedBacklog: BacklogEntry list) (sessionID: string) (errorNotice: string option) : obj array =
    let todoIdxs = foldTodoAnchorsFor host flat
    [ for index in 0 .. foldedBacklog.Length - 1 do
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
          yield
              buildPrefixUserMessage
                  syntheticId
                  finalText
                  (if isNullish todoInfo then sessionID else infoSessionID todoInfo)
                  todoTime ]
    |> List.toArray

let private rebuildVisibleOnly (messages: obj array) (visible: FlatPart list) : obj array =
    let byMessage = visible |> List.groupBy (fun entry -> entry.msgIndex) |> Map.ofList
    [ for msgIdx in 0 .. messages.Length - 1 do
          match Map.tryFind msgIdx byMessage with
          | None -> ()
          | Some entries ->
              let msg = messages.[msgIdx]
              if isNullish msg then ()
              else
                  let originalParts = messageParts msg
                  if isNullish originalParts || not (isArray originalParts) then
                      yield msg
                  else
                      let partMap =
                          entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList
                      let partsArr = originalParts :?> obj array
                      let newParts =
                          [ for partIdx in 0 .. partsArr.Length - 1 do
                                match Map.tryFind partIdx partMap with
                                | Some part -> yield part
                                | None -> () ]
                      if not newParts.IsEmpty then
                          yield withKey msg "parts" (box (List.toArray newParts)) ]
    |> List.toArray

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
            if syntheticPrefixMessages.Length = 0 then rebuilt else Array.concat [| syntheticPrefixMessages; rebuilt |]

let projectMagic (messages: obj array) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : obj array =
    projectMagicFor opencode messages backlog foldAfterFirst sessionID