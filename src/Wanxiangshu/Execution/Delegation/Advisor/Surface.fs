namespace Wanxiangshu.Execution.Delegation.Advisor

open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.Anchor

[<RequireQualifiedAccess>]
module Surface =
    let private arrayOf (value: obj) : obj array =
        if isNull value then [||] else unbox value

    let private requestOf (value: obj) : AdvisorViewRequest =
        let declaration =
            match AnchorSurface.parseDeclaration value?declaration with
            | Ok parsed -> parsed
            | Error failure -> invalidOp (JsFailure.code failure + ": " + JsFailure.reason failure)

        { Messages =
            arrayOf value?messages
            |> Array.toList
            |> List.map (fun message ->
                { Anchor = int (string message?anchor)
                  Text = string message?text })
          Declaration = declaration
          History =
            arrayOf value?history
            |> Array.toList
            |> List.map (fun change ->
                { Anchor = int (string change?anchor)
                  Until = int (string change?until)
                  Summary = string change?summary }) }

    let plan (language: string) (delivered: string array) (request: obj) : obj =
        let planned = AdvisorView.plan language (Set.ofArray delivered) (requestOf request)

        box
            {| viewPrompt = planned.ViewPrompt
               deliveredAnchors = List.toArray planned.DeliveredAnchors
               deliveredMaterial = List.toArray planned.DeliveredMaterial
               hasNewMaterial = planned.HasNewMaterial |}

    let delivered callerSessionId =
        Cursor.delivered callerSessionId |> Set.toArray

    let record callerSessionId (anchors: string array) =
        Cursor.record callerSessionId (List.ofArray anchors)

    let clear callerSessionId = Cursor.clear callerSessionId
    let clearAll () = Cursor.clearAll ()
