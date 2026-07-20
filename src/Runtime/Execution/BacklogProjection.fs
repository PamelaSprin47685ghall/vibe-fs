module Wanxiangshu.Runtime.BacklogProjection

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjectionFold
open Wanxiangshu.Runtime.BacklogProjectionRebuild

type FoldStrategy = BacklogProjectionFold.FoldStrategy
type FoldRange = BacklogProjectionFold.FoldRange

let findFoldRangeFor = BacklogProjectionFold.findFoldRangeFor
let findFoldRange = BacklogProjectionFold.findFoldRange

type BacklogProjectionResult<'raw> =
    { Messages: Message<'raw> list
      TotalTodoOrdinal: int
      FoldFrontierOrdinal: int
      RemainingTodoWritesUntilFold: int
      DidAdvanceFoldFrontier: bool }

let private noFoldResult
    (messages: Message<'raw> list)
    (totalOrdinal: int)
    (minResults: int)
    : BacklogProjectionResult<'raw> =
    { Messages = messages
      TotalTodoOrdinal = totalOrdinal
      FoldFrontierOrdinal = 0
      RemainingTodoWritesUntilFold = max 0 (minResults - totalOrdinal)
      DidAdvanceFoldFrontier = false }

let private projectWithFold
    (host: Host)
    (messages: Message<'raw> list)
    (flat: FlatPart<'raw> list)
    (backlog: BacklogEntry list)
    (range: FoldRange)
    (sessionID: string)
    (totalOrdinal: int)
    (minResults: int)
    : BacklogProjectionResult<'raw> =
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

    let visible = buildVisibleParts host flat range projectionPart

    let rebuilt = rebuildVisibleOnly messages visible

    let projectedMessages =
        if syntheticPrefixMessages.IsEmpty then
            rebuilt
        else
            syntheticPrefixMessages @ rebuilt

    let foldFrontierOrdinal = range.secondToLast + 1
    let remainingUntilFold = max 0 (minResults - (totalOrdinal - foldFrontierOrdinal))

    { Messages = projectedMessages
      TotalTodoOrdinal = totalOrdinal
      FoldFrontierOrdinal = foldFrontierOrdinal
      RemainingTodoWritesUntilFold = remainingUntilFold
      DidAdvanceFoldFrontier = foldFrontierOrdinal > 0 }

let projectBacklogFor
    (host: Host)
    (messages: Message<'raw> list)
    (backlog: BacklogEntry list)
    (strategy: FoldStrategy)
    (sessionID: string)
    : BacklogProjectionResult<'raw> =
    let minResults = requiredFoldAnchorCount strategy

    if messages.IsEmpty then
        noFoldResult messages 0 minResults
    else
        let flat = flatten messages
        let todoIdxs = foldTodoAnchorsFor host flat
        let totalOrdinal = todoIdxs.Length

        match findFoldRangeFor host flat strategy with
        | None -> noFoldResult messages totalOrdinal minResults
        | Some range -> projectWithFold host messages flat backlog range sessionID totalOrdinal minResults

let projectBacklog
    (messages: Message<'raw> list)
    (backlog: BacklogEntry list)
    (strategy: FoldStrategy)
    (sessionID: string)
    : BacklogProjectionResult<'raw> =
    projectBacklogFor opencode messages backlog strategy sessionID
