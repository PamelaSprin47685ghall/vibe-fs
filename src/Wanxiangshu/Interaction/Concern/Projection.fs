namespace Wanxiangshu.Interaction.Concern

open Wanxiangshu.Foundation.Identity

type ConcernMailbox =
    { Id: string
      Concern: string
      Generation: string
      OwnerSessionId: SessionId
      Active: bool }

type ConcernMessage =
    { OccurrenceId: string
      Generation: string
      Id: string
      SenderSessionId: SessionId
      Message: string }

type ConcernProjectionState =
    { Addresses: Map<string, string>
      Mailboxes: Map<string, ConcernMailbox>
      KnownGenerations: Map<string, ConcernMailbox>
      Messages: Map<string, ConcernMessage>
      AnnouncementCoverage: Set<string * SessionId>
      DeliveryCoverage: Set<string> }

type ConcernPreparedFragments =
    { Batch: ConcernPlacementBatch
      Announcements: (string * string) list
      Messages: (string * string) list }

[<RequireQualifiedAccess>]
module ConcernProjection =

    let empty =
        { Addresses = Map.empty
          Mailboxes = Map.empty
          KnownGenerations = Map.empty
          Messages = Map.empty
          AnnouncementCoverage = Set.empty
          DeliveryCoverage = Set.empty }

    let activeMailbox id (state: ConcernProjectionState) =
        Map.tryFind id state.Mailboxes |> Option.filter _.Active

    let tryFindMessage occurrenceId (state: ConcernProjectionState) = Map.tryFind occurrenceId state.Messages

    let subscribe owner occurrenceId id concern (state: ConcernProjectionState) =
        let semanticConflict =
            Map.tryFind id state.Addresses
            |> Option.exists (fun existing -> existing <> concern)

        if semanticConflict then
            Error "concern id is permanently bound to another concern"
        else
            match activeMailbox id state with
            | Some mailbox when mailbox.OwnerSessionId = owner && mailbox.Concern = concern -> Ok None
            | Some _ -> Error "concern id already has a live owner"
            | None ->
                Ok(
                    Some(
                        ConcernFactCases.MailboxSubscribed
                            {| Id = id
                               Concern = concern
                               Generation = occurrenceId
                               OwnerSessionId = owner |}
                    )
                )

    let publish sender occurrenceId id message (state: ConcernProjectionState) =
        match activeMailbox id state with
        | None -> Error "concern id has no live mailbox"
        | Some mailbox ->
            Ok(
                ConcernFactCases.MessagePublished
                    {| OccurrenceId = occurrenceId
                       Generation = mailbox.Generation
                       Id = id
                       SenderSessionId = sender
                       Message = message |}
            )

    let private sameMailbox (left: ConcernMailbox) (right: ConcernMailbox) =
        left.Id = right.Id
        && left.Concern = right.Concern
        && left.Generation = right.Generation
        && left.OwnerSessionId = right.OwnerSessionId

    let private sameMessage (left: ConcernMessage) (right: ConcernMessage) =
        left.OccurrenceId = right.OccurrenceId
        && left.Generation = right.Generation
        && left.Id = right.Id
        && left.SenderSessionId = right.SenderSessionId
        && left.Message = right.Message

    let applyFact fact (state: ConcernProjectionState) : Result<ConcernProjectionState, string> =
        match fact with
        | ConcernFactCases.MailboxSubscribed payload ->
            let mailbox =
                { Id = payload.Id
                  Concern = payload.Concern
                  Generation = payload.Generation
                  OwnerSessionId = payload.OwnerSessionId
                  Active = true }

            match Map.tryFind payload.Generation state.KnownGenerations with
            | Some known when sameMailbox known mailbox -> Ok state
            | Some _ -> Error "generation identity conflict"
            | None ->
                match Map.tryFind payload.Id state.Addresses, activeMailbox payload.Id state with
                | Some concern, _ when concern <> payload.Concern -> Error "concern identity conflict"
                | _, Some _ -> Error "mailbox already active"
                | _ ->
                    Ok
                        { state with
                            Addresses = Map.add payload.Id payload.Concern state.Addresses
                            Mailboxes = Map.add payload.Id mailbox state.Mailboxes
                            KnownGenerations = Map.add payload.Generation mailbox state.KnownGenerations }
        | ConcernFactCases.MessagePublished payload ->
            let message =
                { OccurrenceId = payload.OccurrenceId
                  Generation = payload.Generation
                  Id = payload.Id
                  SenderSessionId = payload.SenderSessionId
                  Message = payload.Message }

            match Map.tryFind payload.OccurrenceId state.Messages with
            | Some known when sameMessage known message -> Ok state
            | Some _ -> Error "message occurrence identity conflict"
            | None ->
                match activeMailbox payload.Id state with
                | Some mailbox when mailbox.Generation = payload.Generation ->
                    Ok { state with Messages = Map.add payload.OccurrenceId message state.Messages }
                | _ -> Error "published generation is no longer live"
        | ConcernFactCases.MailboxRetired payload ->
            match Map.tryFind payload.Id state.Mailboxes with
            | Some mailbox when mailbox.Generation = payload.Generation ->
                Ok { state with Mailboxes = Map.add payload.Id { mailbox with Active = false } state.Mailboxes }
            | _ when Map.containsKey payload.Generation state.KnownGenerations -> Ok state
            | _ -> Error "unknown mailbox generation"

    let prepareFragments recipient (state: ConcernProjectionState) =
        let announcements =
            state.Mailboxes
            |> Map.values
            |> Seq.filter _.Active
            |> Seq.filter (fun mailbox ->
                not (Set.contains (mailbox.Generation, recipient) state.AnnouncementCoverage))
            |> Seq.sortBy _.Id
            |> Seq.toList

        let activeGenerations =
            state.Mailboxes
            |> Map.values
            |> Seq.filter (fun mailbox -> mailbox.Active && mailbox.OwnerSessionId = recipient)
            |> Seq.map _.Generation
            |> Set.ofSeq

        let messages =
            state.Messages
            |> Map.values
            |> Seq.filter (fun message ->
                Set.contains message.Generation activeGenerations
                && not (Set.contains message.OccurrenceId state.DeliveryCoverage))
            |> Seq.sortBy _.OccurrenceId
            |> Seq.toList

        let batch =
            { AnnouncedGenerations = announcements |> List.map _.Generation
              DeliveredMessages = messages |> List.map _.OccurrenceId }

        { Batch = batch
          Announcements = announcements |> List.map (fun mailbox -> mailbox.Id, mailbox.Concern)
          Messages = messages |> List.map (fun message -> message.Id, message.Message) }

    let applyPlacement recipient batch (state: ConcernProjectionState) : Result<ConcernProjectionState, string> =
        let announcementsValid =
            batch.AnnouncedGenerations
            |> List.forall (fun generation -> Map.containsKey generation state.KnownGenerations)

        let deliveriesValid =
            batch.DeliveredMessages
            |> List.forall (fun occurrence ->
                match Map.tryFind occurrence state.Messages with
                | Some message ->
                    state.Mailboxes
                    |> Map.tryFind message.Id
                    |> Option.exists (fun mailbox ->
                        mailbox.Active
                        && mailbox.Generation = message.Generation
                        && mailbox.OwnerSessionId = recipient)
                | None -> false)

        if not announcementsValid || not deliveriesValid then
            Error "concern placement references stale or unknown material"
        else
            Ok
                { state with
                    AnnouncementCoverage =
                        batch.AnnouncedGenerations
                        |> List.fold
                            (fun covered generation -> Set.add (generation, recipient) covered)
                            state.AnnouncementCoverage
                    DeliveryCoverage =
                        batch.DeliveredMessages
                        |> List.fold (fun covered occurrence -> Set.add occurrence covered) state.DeliveryCoverage }

