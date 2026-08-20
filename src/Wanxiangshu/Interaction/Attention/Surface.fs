namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module AttentionSurface =

    type private BoxedState(state: AttentionProjectionState) =
        member _.State = state

    let private stateOf (value: obj) = (unbox<BoxedState> value).State
    let private boxed state = BoxedState(state) :> obj

    let empty () = boxed AttentionProjection.empty

    let record (session: string) (occurrence: string) (text: string) (state: obj) =
        stateOf state
        |> AttentionProjection.record (SessionId.create session) occurrence text
        |> boxed

    let resurface (session: string) (learningOccurrence: string) (workIds: string array) (state: obj) =
        stateOf state
        |> AttentionProjection.resurface (SessionId.create session) learningOccurrence (Array.toList workIds)
        |> boxed

    let pending (session: string) (state: obj) : obj =
        stateOf state
        |> AttentionProjection.pending (SessionId.create session)
        |> List.map (fun item ->
            box
                {| occurrence = item.OccurrenceId
                   text = item.Text |})
        |> List.toArray
        |> box
