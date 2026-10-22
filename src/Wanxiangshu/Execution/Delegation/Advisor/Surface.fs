namespace Wanxiangshu.Execution.Delegation.Advisor

/// JS adapter over already-resolved carry material and the host-owned delivery cursor.
[<RequireQualifiedAccess>]
module Surface =
    let plan (language: string) (delivered: string array) (material: obj array) : obj =
        let planned = AdvisorView.plan language (Set.ofArray delivered) material

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
