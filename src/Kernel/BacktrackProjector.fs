module VibeFs.Kernel.BacktrackProjector

open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDecoder
open VibeFs.Kernel.BacktrackCodec
open VibeFs.Kernel.PartStream

let backtrackToolName = "backtrack"

let private backtrackAnchor (part: obj) : int option =
    let input = partToolInput part
    if isNullish input then None
    else
        let anchor = get input "anchor"
        if isNullish anchor then None
        else
            match System.Int32.TryParse (string anchor) with
            | true, n -> Some n
            | false, _ -> None

let private backtrackNote (part: obj) : string =
    let input = partToolInput part
    if isNullish input then "" else str input "note"

let private applyRewrite (visible: ResizeArray<FlatPart>) (anchor: int) (note: string) : unit =
    let mutable anchorIdx = -1
    for i = 0 to visible.Count - 1 do
        if anchorIdx >= 0 then ()
        else
            let part = visible.[i].part
            if partIsTool part then
                match tryParseId (partToolOutput part) with
                | Some id when id = anchor -> anchorIdx <- i
                | _ -> ()
    if anchorIdx < 0 then ()
    else
        let anchorEntry = visible.[anchorIdx]
        let rewritten = setPartOutput anchorEntry.part (encodeId anchor note)
        visible.[anchorIdx] <- { anchorEntry with part = rewritten }
        let mutable i = visible.Count - 1
        while i > anchorIdx do
            if not visible.[i].isUser then visible.RemoveAt i
            i <- i - 1

let hasBacktrackEvents (messages: obj array) : bool =
    if isNullish messages then false
    else
        let flat = flatten messages
        flat |> List.exists (fun fp ->
            partIsTool fp.part && partToolName fp.part = backtrackToolName && partToolStatus fp.part = "completed")

let project (messages: obj array) : obj array =
    if isNullish messages then [||]
    elif not (hasBacktrackEvents messages) then messages
    else
        let flat = flatten messages
        let visible = ResizeArray<FlatPart>()
        for entry in flat do
            let part = entry.part
            if partIsTool part && partToolName part = backtrackToolName then
                if partToolStatus part = "completed" then
                    let anchor = backtrackAnchor part
                    let note = backtrackNote part
                    match anchor with
                    | Some a -> applyRewrite visible a note
                    | None -> ()
            else
                visible.Add entry
        rebuild messages (List.ofSeq visible)

let visibleIds (messages: obj array) : int list =
    let flat = flatten messages
    let visible = ResizeArray<FlatPart>()
    for entry in flat do
        let part = entry.part
        if partIsTool part && partToolName part = backtrackToolName then
            if partToolStatus part = "completed" then
                match backtrackAnchor part with
                | Some a -> applyRewrite visible a (backtrackNote part)
                | None -> ()
        else
            visible.Add entry
    visible
    |> Seq.choose (fun entry ->
        let part = entry.part
        if partIsTool part && partToolStatus part = "completed" then
            tryParseId (partToolOutput part)
        else None)
    |> Seq.toList

let maxVisibleId (messages: obj array) : int =
    visibleIds messages |> List.fold max 0
