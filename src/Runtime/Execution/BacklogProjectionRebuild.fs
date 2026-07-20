module Wanxiangshu.Runtime.BacklogProjectionRebuild

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjectionFold

let private messageTimeOrNull (msg: Message<'raw>) : 'raw = msg.info.time

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

let buildSyntheticPrefixMessages
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

let rebuildVisibleOnly (messages: Message<'raw> list) (visible: FlatPart<'raw> list) : Message<'raw> list =
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

let buildVisibleParts
    (host: Host)
    (flat: FlatPart<'raw> list)
    (range: FoldRange)
    (projectionPart: Part<'raw>)
    : FlatPart<'raw> list =
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
