module Wanxiangshu.Kernel.BacklogProjection

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Message

[<RequireQualifiedAccess>]
type FoldStrategy =
    | FoldAfterFirst
    | FoldAfterSecond

let private isFoldAnchorFor (host: Host) (part: Part<'raw>) : bool = isTodoResultFor host part

type FoldRange = { firstResult: int; secondToLast: int }

let private todoIndexesFor (host: Host) (flat: FlatPart<'raw> list) : int list =
    flat
    |> List.indexed
    |> List.choose (fun (index, fp) -> if isFoldAnchorFor host fp.part then Some index else None)

let private foldTodoAnchorsFor (host: Host) (flat: FlatPart<'raw> list) : int list = todoIndexesFor host flat

let private requiredFoldAnchorCount (strategy: FoldStrategy) : int =
    match strategy with
    | FoldStrategy.FoldAfterFirst -> 2
    | FoldStrategy.FoldAfterSecond -> 3

let private messageTimeOrNull (msg: Message<'raw>) : 'raw = msg.info.time

let private collectUserText (flat: FlatPart<'raw> list) (fromIdx: int) (toIdx: int) : string list =
    let lo = max 0 fromIdx
    let hi = min (flat.Length - 1) toIdx

    if lo > hi then
        []
    else
        flat.[lo..hi]
        |> List.choose (fun fp ->
            if fp.isUser && partIsText fp.part then
                let t = (partTextStr fp.part).Trim()
                if t <> "" then Some t else None
            else
                None)

let findFoldRangeFor (host: Host) (flat: FlatPart<'raw> list) (strategy: FoldStrategy) : FoldRange option =
    let todoIdxs = foldTodoAnchorsFor host flat
    let minResults = requiredFoldAnchorCount strategy

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

let findFoldRange (flat: FlatPart<'raw> list) (strategy: FoldStrategy) : FoldRange option =
    findFoldRangeFor opencode flat strategy

let private buildPrefixUserMessage (id: string) (text: string) (sessionID: string) (time: 'raw) : Message<'raw> =
    { info =
        { id = id
          sessionID = sessionID
          role = User
          agent = "orchestrator"
          isError = false
          toolName = ""
          details = Unchecked.defaultof<'raw>
          time = time }
      parts = [ TextPart text ]
      source = Native
      raw = Unchecked.defaultof<'raw> }

let private buildSyntheticPrefixMessages
    (host: Host)
    (messages: Message<'raw> list)
    (flat: FlatPart<'raw> list)
    (foldedBacklog: BacklogEntry list)
    (sessionID: string)
    (errorNotice: string option)
    : Message<'raw> list =
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
          let syntheticId = backlogPrefixIdPrefix + string (index + 1)
          yield buildPrefixUserMessage syntheticId finalText todoMessage.info.sessionID todoTime ]

let private rebuildVisibleOnly (messages: Message<'raw> list) (visible: FlatPart<'raw> list) : Message<'raw> list =
    let byMessage = visible |> List.groupBy (fun entry -> entry.msgIndex) |> Map.ofList

    messages
    |> List.indexed
    |> List.choose (fun (msgIdx, msg) ->
        match Map.tryFind msgIdx byMessage with
        | None -> None
        | Some entries ->
            let partMap =
                entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList

            let newParts =
                msg.parts
                |> List.indexed
                |> List.choose (fun (partIdx, part) -> Map.tryFind partIdx partMap)

            if newParts.IsEmpty then
                None
            else
                Some { msg with parts = newParts })

let projectBacklogFor
    (host: Host)
    (messages: Message<'raw> list)
    (backlog: BacklogEntry list)
    (strategy: FoldStrategy)
    (sessionID: string)
    : Message<'raw> list =
    if messages.IsEmpty then
        messages
    else
        let flat = flatten messages

        match findFoldRangeFor host flat strategy with
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
            let projectionPart = setPartOutputTyped flat.[range.firstResult].part projectionText
            let errorNotice = lastTodoErrorTextFor host flat

            let syntheticPrefixMessages =
                if foldedBacklog.IsEmpty then
                    []
                else
                    buildSyntheticPrefixMessages host messages flat foldedBacklog sessionID errorNotice

            let visible =
                flat
                |> List.indexed
                |> List.choose (fun (i, fp) ->
                    if i < range.firstResult then
                        None
                    elif i = range.firstResult then
                        Some { fp with part = projectionPart }
                    elif i < range.secondToLast then
                        if isReviewTool fp.part then Some fp else None
                    elif isTodoErrorFor host fp.part then
                        None
                    else
                        Some fp)

            let rebuilt = rebuildVisibleOnly messages visible

            if syntheticPrefixMessages.IsEmpty then
                rebuilt
            else
                syntheticPrefixMessages @ rebuilt

let projectBacklog
    (messages: Message<'raw> list)
    (backlog: BacklogEntry list)
    (strategy: FoldStrategy)
    (sessionID: string)
    : Message<'raw> list =
    projectBacklogFor opencode messages backlog strategy sessionID
