namespace Wanxiangshu.Interaction.Concern

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module ConcernSurface =

    type private BoxedState(state: ConcernProjectionState) =
        member _.State = state

    let private stateOf (value: obj) = (unbox<BoxedState> value).State
    let private boxed state = BoxedState(state) :> obj

    let empty () = boxed ConcernProjection.empty

    let private apply fact state =
        match ConcernProjection.applyFact fact state with
        | Ok updated -> box {| ok = true; error = null; state = boxed updated |}
        | Error reason -> box {| ok = false; error = reason; state = boxed state |}

    let subscribe owner occurrence id concern state =
        let current = stateOf state
        match ConcernProjection.subscribe (SessionId.create owner) occurrence id concern current with
        | Error reason -> box {| ok = false; error = reason; state = boxed current; appended = false |}
        | Ok None -> box {| ok = true; error = null; state = boxed current; appended = false |}
        | Ok(Some fact) ->
            match ConcernProjection.applyFact fact current with
            | Ok updated -> box {| ok = true; error = null; state = boxed updated; appended = true |}
            | Error reason -> box {| ok = false; error = reason; state = boxed current; appended = false |}

    let publish sender occurrence id message state =
        let current = stateOf state
        match ConcernProjection.publish (SessionId.create sender) occurrence id message current with
        | Error reason -> box {| ok = false; error = reason; state = boxed current |}
        | Ok fact -> apply fact current

    let applyPublishedClaim sender occurrence id generation message state =
        let current = stateOf state
        ConcernFactCases.MessagePublished
            {| OccurrenceId = occurrence
               Generation = generation
               Id = id
               SenderSessionId = SessionId.create sender
               Message = message |}
        |> fun fact -> apply fact current

    let retire owner id generation state =
        let current = stateOf state
        ConcernFactCases.MailboxRetired
            {| Generation = generation
               Id = id
               OwnerSessionId = SessionId.create owner |}
        |> fun fact -> apply fact current

    let prepare recipient state : obj =
        let prepared = ConcernProjection.prepareFragments (SessionId.create recipient) (stateOf state)
        box
            {| announcements =
                prepared.Announcements
                |> List.map (fun (id, concern) -> box {| id = id; concern = concern |})
                |> List.toArray
               messages =
                prepared.Messages
                |> List.map (fun (id, message) -> box {| id = id; message = message |})
                |> List.toArray
               announcedGenerations = List.toArray prepared.Batch.AnnouncedGenerations
               deliveredMessages = List.toArray prepared.Batch.DeliveredMessages |}

    let place recipient (announcedGenerations: string array) (deliveredMessages: string array) state =
        let current = stateOf state
        let batch =
            { AnnouncedGenerations = Array.toList announcedGenerations
              DeliveredMessages = Array.toList deliveredMessages }

        match ConcernProjection.applyPlacement (SessionId.create recipient) batch current with
        | Ok updated -> box {| ok = true; error = null; state = boxed updated |}
        | Error reason -> box {| ok = false; error = reason; state = boxed current |}

