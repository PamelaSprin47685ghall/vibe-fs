module Wanxiangshu.Runtime.BacklogProjectionFold

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Backlog.BacklogTypes

[<RequireQualifiedAccess>]
type FoldStrategy =
    | FoldAfterFirst
    | FoldAfterSecond

type FoldRange = { firstResult: int; secondToLast: int }

let private isFoldAnchorFor (host: Host) (part: Part<'raw>) : bool = isTodoResultFor host part

let private todoIndexesFor (host: Host) (flat: FlatPart<'raw> list) : int list =
    flat
    |> List.indexed
    |> List.choose (fun (index, fp) -> if isFoldAnchorFor host fp.part then Some index else None)

let foldTodoAnchorsFor (host: Host) (flat: FlatPart<'raw> list) : int list = todoIndexesFor host flat

let requiredFoldAnchorCount (strategy: FoldStrategy) : int =
    match strategy with
    | FoldStrategy.FoldAfterFirst -> 2
    | FoldStrategy.FoldAfterSecond -> 3

let collectUserText (flat: FlatPart<'raw> list) (fromIdx: int) (toIdx: int) : string list =
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

        if secondToLast < firstResult then
            None
        else
            Some
                { firstResult = firstResult
                  secondToLast = secondToLast }

let findFoldRange (flat: FlatPart<'raw> list) (strategy: FoldStrategy) : FoldRange option =
    findFoldRangeFor opencode flat strategy
