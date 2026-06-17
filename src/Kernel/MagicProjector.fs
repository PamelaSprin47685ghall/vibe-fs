module VibeFs.Kernel.MagicProjector

open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDecoder
open VibeFs.Kernel.MagicTypes
open VibeFs.Kernel.PartStream
open VibeFs.Kernel.SyntheticIds

let isTodoResult (part: obj) : bool =
    partIsTool part && partToolName part = magicTodoToolName && partToolStatus part = "completed"

let isTodoError (part: obj) : bool =
    partIsTool part && partToolName part = magicTodoToolName && partToolStatus part = "error"

let private emptyBacklogText = "\u3010Magic Todo Backlog\u3011\n\u5f53\u524d\u8fd8\u6ca1\u6709\u5df2\u5b8c\u6210\u5de5\u4f5c\u62a5\u544a\u3002"
let private userMsgHeader = "[\u7528\u6237\u5728\u5de5\u4f5c\u671f\u95f4\u53d1\u9001\u7684\u6d88\u606f]"
let private foldHeader = "[\u5df2\u5b8c\u6210\u5e76\u6298\u53e0\u7684\u5de5\u4f5c\u8bb0\u5f55] \u4ee5\u4e0b\u62a5\u544a\u6765\u81ea\u88ab\u6298\u53e0\u7684\u65e7\u8f6e\u6b21\uff0c\u76f8\u5173\u6587\u4ef6\u5df2\u5199\u5165\u78c1\u76d8"
let private sectionSep = "\n\n---\n\n"
let private lineSep = "\n\n"
let private dotSep = " \u00b7 "
let private errorPrefix = "[\u4e0a\u6b21\u64cd\u4f5c\u5931\u8d25] "

let buildBacklogText (backlog: BacklogEntry list) (userPrompts: string list) : string =
    if backlog.IsEmpty && userPrompts.IsEmpty then emptyBacklogText
    else
        let parts = ResizeArray<string>()
        if userPrompts.Length > 0 then
            let joined = String.concat lineSep userPrompts
            parts.Add(userMsgHeader + "\n" + joined)
        if not backlog.IsEmpty then
            let reports = backlog |> List.map (fun entry ->
                let ts = if entry.timestamp <> "" then dotSep + entry.timestamp else ""
                "#" + string entry.sequence + ts + "\n" + entry.report)
            parts.Add(foldHeader + "\n" + String.concat sectionSep reports)
        String.concat sectionSep parts

let private lastTodoErrorText (flat: FlatPart list) : string option =
    let mutable last = None
    for fp in flat do
        if isTodoError fp.part then
            last <- Some (partToolError fp.part)
    last

let private collectUserText (flat: FlatPart list) (fromIdx: int) (toIdx: int) : string list =
    let result = ResizeArray<string>()
    for i = fromIdx to toIdx do
        if i >= 0 && i < flat.Length then
            let fp = flat.[i]
            if fp.isUser && partIsText fp.part then
                let text = partTextStr fp.part
                if text.Trim() <> "" then result.Add(text.Trim())
    List.ofSeq result

type FoldRange = { firstResult: int; secondToLast: int }

let findFoldRange (flat: FlatPart list) (foldAfterFirst: bool) : FoldRange option =
    let todoIdxs = flat |> List.indexed |> List.choose (fun (i, fp) ->
        if isTodoResult fp.part then Some i else None)
    let minResults = if foldAfterFirst then 2 else 3
    if todoIdxs.Length < minResults then None
    else
        let firstResult = todoIdxs.[0]
        let secondToLast = todoIdxs.[todoIdxs.Length - 2]
        if secondToLast <= firstResult then None
        else Some { firstResult = firstResult; secondToLast = secondToLast }

let private buildPrefixUserMessage (text: string) (sessionID: string) : obj =
    box (createObj [
        "info", box (createObj [
            "id", box (magicTodoPrefixPrefix + "1")
            "sessionID", box sessionID
            "role", box "user"
            "time", box (createObj [ "created", box 0 ])
            "agent", box "orchestrator"
            "model", box (createObj [ "providerID", box ""; "modelID", box "" ])
        ])
        "parts", box [| box {| ``type`` = "text"; text = text |} |]
    ])

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
                if isNullish originalParts || not (isArray originalParts) then result.Add msg
                else
                    let partMap = entries |> List.map (fun entry -> entry.partIndex, entry.part) |> Map.ofList
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

let projectMagic (messages: obj array) (backlog: BacklogEntry list) (foldAfterFirst: bool) (sessionID: string) : obj array =
    if isNullish messages then [||]
    else
        let flat = flatten messages
        match findFoldRange flat foldAfterFirst with
        | None -> messages
        | Some range ->
            let foldedBacklog = if backlog.Length > 0 then backlog.[.. backlog.Length - 2] else []
            let middleUserText = collectUserText flat (range.firstResult + 1) (range.secondToLast - 1)
            let projectionText = buildBacklogText foldedBacklog middleUserText
            let projectionPart = setPartOutput flat.[range.firstResult].part projectionText
            let prefixUserText = collectUserText flat 0 (range.firstResult - 1)
            let errorNotice = lastTodoErrorText flat
            let hasPrefix = range.firstResult > 0 && not foldedBacklog.IsEmpty && not prefixUserText.IsEmpty
            let visible = ResizeArray<FlatPart>()
            for i = 0 to flat.Length - 1 do
                let fp = flat.[i]
                if i < range.firstResult then ()
                elif i = range.firstResult then visible.Add { fp with part = projectionPart }
                elif i < range.secondToLast then ()
                elif isTodoError fp.part then ()
                else visible.Add fp
            let rebuilt = rebuildVisibleOnly messages (List.ofSeq visible)
            if not hasPrefix then rebuilt
            else
                let prefixParts = ResizeArray<string>()
                prefixParts.Add(buildBacklogText [ foldedBacklog.[0] ] prefixUserText)
                match errorNotice with
                | Some err when err <> "" -> prefixParts.Add(errorPrefix + err)
                | _ -> ()
                let prefixMsg = buildPrefixUserMessage (String.concat sectionSep prefixParts) sessionID
                Array.concat [| [| prefixMsg |]; rebuilt |]
