namespace Wanxiangshu.Execution.Delegation.Advisor

open Fable.Core.JsInterop

/// advisor-[002]/[003]: JS-native owner boundary for the advisor context view and the
/// process-live delivered-anchor cursor. Plain JS shapes cross here; `AdvisorView` keeps the
/// selection, delta and prompt-rendering semantics.
[<RequireQualifiedAccess>]
module Surface =

    let private text (value: obj) : string =
        if isNull value then "" else string value

    let private number (value: obj) : int =
        if isNull value then 0 else int (string value)

    let private arrayOf (value: obj) : obj array =
        if isNull value then [||] else value :?> obj array

    let private messageOf (value: obj) : AdvisorViewMessage =
        { Anchor = number (value?anchor)
          Text = text (value?text) }

    /// advisor-[003]: a `ThenRemove` pair crosses as a two-slot array, `[anchor, summary]`.
    let private removalOf (value: obj) : int * string =
        let pair = arrayOf value
        (number pair.[0], text pair.[1])

    let private requestOf (value: obj) : AdvisorViewRequest =
        if isNull value then
            { Messages = []
              Declaration = { PrefixUntil = 0; ThenRemove = [] } }
        else
            let declaration = value?declaration

            let prefixUntil =
                if isNull declaration then
                    0
                else
                    number (declaration?prefixUntil)

            let thenRemove =
                if isNull declaration then
                    []
                else
                    arrayOf (declaration?thenRemove) |> Array.toList |> List.map removalOf

            { Messages = arrayOf (value?messages) |> Array.toList |> List.map messageOf
              Declaration =
                { PrefixUntil = prefixUntil
                  ThenRemove = thenRemove } }

    let plan (language: string) (delivered: int array) (request: obj) : obj =
        let planned = AdvisorView.plan language (Set.ofArray delivered) (requestOf request)

        box
            {| viewPrompt = planned.ViewPrompt
               deliveredAnchors = List.toArray planned.DeliveredAnchors
               hasNewMaterial = planned.HasNewMaterial |}

    /// The F# set order is ascending, so the array hands the caller a sorted cursor.
    let delivered (callerSessionId: string) : int array =
        Cursor.delivered callerSessionId |> Set.toList |> List.toArray

    let record (callerSessionId: string) (anchors: int array) : unit =
        Cursor.record callerSessionId (List.ofArray anchors)

    let clear (callerSessionId: string) : unit = Cursor.clear callerSessionId

    let clearAll () : unit = Cursor.clearAll ()
