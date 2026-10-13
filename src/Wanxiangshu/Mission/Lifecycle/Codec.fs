namespace Wanxiangshu.Mission.Lifecycle

open Thoth.Json

[<RequireQualifiedAccess>]
type ReadFault =
    | StorageInvalid of string
    | PairIncomplete of string
    | Rejected of string

[<RequireQualifiedAccess>]
module LifecycleCodec =

    let eventTypeOf (event: LifecycleEvent) =
        match event with
        | LifecycleEvent.WorkOpened _ -> LifecycleEventTypes.WorkOpened
        | LifecycleEvent.DevOpsBound _ -> LifecycleEventTypes.DevOpsBound
        | LifecycleEvent.IncumbencyOpened _ -> LifecycleEventTypes.IncumbencyOpened
        | LifecycleEvent.ConfirmationCommitted _ -> LifecycleEventTypes.ConfirmationCommitted
        | LifecycleEvent.IncumbencyRetiredOneshot _
        | LifecycleEvent.IncumbencyRetiredExecution _ -> LifecycleEventTypes.IncumbencyRetired
        | LifecycleEvent.Reviewed _ -> LifecycleEventTypes.Reviewed
        | LifecycleEvent.Reopened _ -> LifecycleEventTypes.Reopened
        | LifecycleEvent.AdvanceCommitted _ -> LifecycleEventTypes.AdvanceCommitted
        | LifecycleEvent.RetirementBlocked _ -> LifecycleEventTypes.RetirementBlocked

    let private stringField (name: string) (value: string) = name, Encode.string value

    let private sequenceField (sequence: WorkSequence) =
        "sequence", Encode.list (WorkSequence.render sequence |> List.map Encode.string)

    let private cutField (cut: ProjectionCut) =
        "projectionCut",
        Encode.object
            [ "providerRunId", Encode.string cut.ProviderRunId
              "toolCallId", Encode.string cut.ToolCallId ]

    let encodePayload (event: LifecycleEvent) : JsonValue =
        match event with
        | LifecycleEvent.WorkOpened(workId, root, sequence, authorityInitial) ->
            Encode.object
                [ stringField "authorityInitial" authorityInitial
                  stringField "root" root
                  sequenceField sequence
                  stringField "workId" workId ]
        | LifecycleEvent.DevOpsBound(workId, devopsId, target) ->
            Encode.object
                [ stringField "devopsId" devopsId
                  "target",
                  match target with
                  | Some value -> Encode.string value
                  | None -> Encode.nil
                  stringField "workId" workId ]
        | LifecycleEvent.IncumbencyOpened(workId, incumbencyId, stage, openingCursor) ->
            Encode.object
                [ stringField "incumbencyId" incumbencyId
                  "openingCursor", Encode.int64 openingCursor
                  stringField "stage" (StageElement.render stage)
                  stringField "workId" workId ]
        | LifecycleEvent.ConfirmationCommitted(incumbencyId, providerRunId, toolCallId) ->
            Encode.object
                [ stringField "incumbencyId" incumbencyId
                  stringField "providerRunId" providerRunId
                  stringField "toolCallId" toolCallId ]
        | LifecycleEvent.IncumbencyRetiredOneshot retired ->
            let fields =
                [ yield stringField "expectedAdvance" retired.ExpectedAdvance
                  match retired.Digest with
                  | Some digest -> yield stringField "digest" digest
                  | None -> ()
                  match retired.ExecSessionId with
                  | Some execSessionId -> yield stringField "execSessionId" execSessionId
                  | None -> ()
                  yield stringField "incumbencyId" retired.IncumbencyId
                  yield stringField "outcome" (RetirementOutcome.render retired.Outcome)
                  match retired.Path with
                  | Some path -> yield stringField "path" path
                  | None -> ()
                  yield stringField "providerRunId" retired.ProviderRunId
                  yield "retirementCursor", Encode.int64 retired.RetirementCursor
                  yield stringField "retirementId" retired.RetirementId
                  yield stringField "toolCallId" retired.ToolCallId ]

            Encode.object fields
        | LifecycleEvent.IncumbencyRetiredExecution retired ->
            Encode.object
                [ stringField "expectedAdvance" retired.ExpectedAdvance
                  stringField "incumbencyId" retired.IncumbencyId
                  stringField "outcome" (RetirementOutcome.render retired.Outcome)
                  cutField retired.ProjectionCut
                  stringField "retirementId" retired.RetirementId ]
        | LifecycleEvent.Reviewed(incumbencyId, stage, outcome) ->
            Encode.object
                [ stringField "incumbencyId" incumbencyId
                  stringField "outcome" (ReviewOutcome.render outcome)
                  stringField "stage" (StageElement.render stage) ]
        | LifecycleEvent.Reopened incumbencyId ->
            Encode.object [ stringField "incumbencyId" incumbencyId ]
        | LifecycleEvent.AdvanceCommitted(incumbencyId, expected, next) ->
            Encode.object
                [ stringField "expected" expected
                  stringField "incumbencyId" incumbencyId
                  stringField "next" next ]
        | LifecycleEvent.RetirementBlocked(incumbencyId, blockerDigest) ->
            Encode.object
                [ stringField "blockerDigest" blockerDigest
                  stringField "incumbencyId" incumbencyId ]

    let private requireString (name: string) : Decoder<string> = Decode.field name Decode.string

    let private absentOrString (name: string) : Decoder<string option> =
        Decode.keyValuePairs Decode.value
        |> Decode.andThen (fun pairs ->
            match List.tryFind (fun (key, _) -> key = name) pairs with
            | None -> Decode.succeed None
            | Some(_, value) ->
                match Decode.fromValue ("$." + name) (Decode.option Decode.string) value with
                | Ok text -> Decode.succeed text
                | Error _ -> Decode.fail (name + " is present but not a string")
            )

    let private forbid (names: string list) : Decoder<unit> =
        Decode.object (fun get ->
            names
            |> List.iter (fun name ->
                match get.Optional.Field name Decode.string with
                | Some _ -> failwith ("forbidden field " + name)
                | None -> ())
            ())

    let private decodeSequence : Decoder<WorkSequence> =
        Decode.field "sequence" (Decode.list Decode.string)
        |> Decode.andThen (fun rendered ->
            let parsed = rendered |> List.map StageElement.parse
            if parsed |> List.exists Option.isNone then
                Decode.fail "sequence element is not S1, S2, S3 or S4"
            else
                match parsed |> List.choose id |> WorkSequence.ofElements with
                | Some sequence -> Decode.succeed sequence
                | None -> Decode.fail "sequence length is neither 1 nor 3")

    let private decodeWorkOpened : Decoder<LifecycleEvent> =
        Decode.map4
            (fun authority root sequence workId ->
                LifecycleEvent.WorkOpened(workId, root, sequence, authority))
            (requireString "authorityInitial")
            (requireString "root")
            decodeSequence
            (requireString "workId")

    let private decodeStage (name: string) : Decoder<StageElement> =
        requireString name
        |> Decode.andThen (fun value ->
            match StageElement.parse value with
            | Some stage -> Decode.succeed stage
            | None -> Decode.fail (name + " is not a stage"))

    let private decodeOutcome (name: string) : Decoder<RetirementOutcome> =
        requireString name
        |> Decode.andThen (fun value ->
            match RetirementOutcome.parse value with
            | Some outcome -> Decode.succeed outcome
            | None -> Decode.fail "outcome is neither Relayed nor Delivered")

    let private decodeReviewOutcome : Decoder<ReviewOutcome> =
        requireString "outcome"
        |> Decode.andThen (fun value ->
            match ReviewOutcome.parse value with
            | Some outcome -> Decode.succeed outcome
            | None -> Decode.fail "review outcome is neither Promoted nor Stayed")

    let private decodeCut : Decoder<ProjectionCut> =
        Decode.field "projectionCut" (
            Decode.map2
                (fun providerRunId toolCallId ->
                    { ProviderRunId = providerRunId
                      ToolCallId = toolCallId })
                (Decode.field "providerRunId" Decode.string)
                (Decode.field "toolCallId" Decode.string)
        )

    let private hasField (payload: JsonValue) (name: string) =
        match Decode.fromValue "$" (Decode.keyValuePairs Decode.value) payload with
        | Ok pairs -> List.exists (fun (key, _) -> key = name) pairs
        | Error _ -> false

    let decodePayload (eventType: string) (payload: JsonValue) : Result<LifecycleEvent, string> =
        let run (decoder: Decoder<LifecycleEvent>) =
            Decode.fromValue "$" decoder payload

        if eventType = LifecycleEventTypes.WorkOpened then
            if hasField payload "confirmationCount"
               || hasField payload "spawn"
               || hasField payload "autoDispatch"
               || hasField payload "snapshotId" then
                Error "opening payload contains a forbidden field"
            else
                run decodeWorkOpened
        elif eventType = LifecycleEventTypes.DevOpsBound then
            run (
                Decode.map3
                    (fun devopsId target workId -> LifecycleEvent.DevOpsBound(workId, devopsId, target))
                    (requireString "devopsId")
                    (absentOrString "target")
                    (requireString "workId")
            )
        elif eventType = LifecycleEventTypes.IncumbencyOpened then
            if hasField payload "snapshotId" then
                Error "opening payload contains snapshotId"
            else
                run (
                    Decode.map4
                        (fun incumbencyId openingCursor stage workId ->
                            LifecycleEvent.IncumbencyOpened(workId, incumbencyId, stage, openingCursor))
                        (requireString "incumbencyId")
                        (Decode.field "openingCursor" Decode.int64)
                        (decodeStage "stage")
                        (requireString "workId")
                )
        elif eventType = LifecycleEventTypes.ConfirmationCommitted then
            run (
                Decode.map3
                    (fun incumbencyId providerRunId toolCallId ->
                        LifecycleEvent.ConfirmationCommitted(incumbencyId, providerRunId, toolCallId))
                    (requireString "incumbencyId")
                    (requireString "providerRunId")
                    (requireString "toolCallId")
            )
        elif eventType = LifecycleEventTypes.IncumbencyRetired then
            if hasField payload "retirementCursor" && hasField payload "projectionCut" then
                Error "retirement payload mixes retirementCursor and projectionCut"
            elif hasField payload "retirementCursor" then
                run (
                    Decode.map8
                        (fun digest execSessionId expected incumbency outcome path provider tool ->
                            fun cursor retirementId ->
                                LifecycleEvent.IncumbencyRetiredOneshot
                                    { RetirementId = retirementId
                                      IncumbencyId = incumbency
                                      Outcome = outcome
                                      ProviderRunId = provider
                                      ToolCallId = tool
                                      ExpectedAdvance = expected
                                      RetirementCursor = cursor
                                      ExecSessionId = execSessionId
                                      Digest = digest
                                      Path = path })
                        (absentOrString "digest")
                        (absentOrString "execSessionId")
                        (requireString "expectedAdvance")
                        (requireString "incumbencyId")
                        (decodeOutcome "outcome")
                        (absentOrString "path")
                        (requireString "providerRunId")
                        (requireString "toolCallId")
                    |> Decode.andThen (fun build ->
                        Decode.map2
                            build
                            (Decode.field "retirementCursor" Decode.int64)
                            (requireString "retirementId"))
                )
            elif hasField payload "projectionCut" then
                if hasField payload "digest" || hasField payload "path" || hasField payload "execSessionId" then
                    Error "execution retirement payload contains a delivery field"
                else
                    run (
                        Decode.map5
                            (fun expected incumbency outcome cut retirementId ->
                                LifecycleEvent.IncumbencyRetiredExecution
                                    { RetirementId = retirementId
                                      IncumbencyId = incumbency
                                      Outcome = outcome
                                      ProjectionCut = cut
                                      ExpectedAdvance = expected })
                            (requireString "expectedAdvance")
                            (requireString "incumbencyId")
                            (decodeOutcome "outcome")
                            decodeCut
                            (requireString "retirementId")
                    )
            else
                Error "retirement payload has neither retirementCursor nor projectionCut"
        elif eventType = LifecycleEventTypes.Reviewed then
            if hasField payload "findings" || hasField payload "snapshotId" then
                Error "review payload contains a forbidden field"
            else
                run (
                    Decode.map3
                        (fun incumbency outcome stage -> LifecycleEvent.Reviewed(incumbency, stage, outcome))
                        (requireString "incumbencyId")
                        decodeReviewOutcome
                        (decodeStage "stage")
                )
        elif eventType = LifecycleEventTypes.Reopened then
            run (Decode.map LifecycleEvent.Reopened (requireString "incumbencyId"))
        elif eventType = LifecycleEventTypes.AdvanceCommitted then
            if hasField payload "snapshotId" || hasField payload "messageId" then
                Error "advance payload contains a forbidden field"
            else
                run (
                    Decode.map3
                        (fun expected incumbency next -> LifecycleEvent.AdvanceCommitted(incumbency, expected, next))
                        (requireString "expected")
                        (requireString "incumbencyId")
                        (requireString "next")
                )
        elif eventType = LifecycleEventTypes.RetirementBlocked then
            run (
                Decode.map2
                    (fun blocker incumbency -> LifecycleEvent.RetirementBlocked(incumbency, blocker))
                    (requireString "blockerDigest")
                    (requireString "incumbencyId")
            )
        else
            Error("unknown lifecycle event " + eventType)

    let encodeLine (event: LifecycleEvent) : string =
        let envelope =
            Encode.object
                [ "event_type", Encode.string (eventTypeOf event)
                  "payload", encodePayload event ]

        Encode.toString 0 envelope

    let private pairIncomplete (events: LifecycleEvent list) =
        let rec walk (index: int) =
            if index >= events.Length then
                None
            else
                match events.[index] with
                | LifecycleEvent.WorkOpened _ ->
                    match events |> List.tryItem (index + 1) with
                    | Some(LifecycleEvent.IncumbencyOpened _) -> walk (index + 2)
                    | _ -> Some "WorkOpened without the first IncumbencyOpened"
                | LifecycleEvent.IncumbencyRetiredOneshot retired when retired.Outcome = RetirementOutcome.Relayed ->
                    match events |> List.tryItem (index + 1) with
                    | Some(LifecycleEvent.IncumbencyOpened _) -> walk (index + 2)
                    | _ -> Some "Relayed retirement without the successor IncumbencyOpened"
                | LifecycleEvent.IncumbencyRetiredExecution retired when retired.Outcome = RetirementOutcome.Relayed ->
                    match events |> List.tryItem (index + 1) with
                    | Some(LifecycleEvent.IncumbencyOpened _) -> walk (index + 2)
                    | _ -> Some "Relayed retirement without the successor IncumbencyOpened"
                | _ -> walk (index + 1)

        walk 0

    let readDocument (text: string) : Result<LifecycleEvent list, ReadFault> =
        if text.Length = 0 || not (text.EndsWith "\n") then
            Error(ReadFault.StorageInvalid "lifecycle document does not end with a newline")
        else
            let lines =
                text.Split [| '\n' |]
                |> Array.toList
                |> List.filter (fun line -> line <> "")

            let decoded =
                lines
                |> List.fold
                    (fun state line ->
                        match state with
                        | Error _ -> state
                        | Ok events ->
                            match Decode.fromString (Decode.field "event_type" Decode.string) line with
                            | Error error -> Error(ReadFault.StorageInvalid error)
                            | Ok eventType ->
                                match Decode.fromString (Decode.field "payload" Decode.value) line with
                                | Error error -> Error(ReadFault.StorageInvalid error)
                                | Ok payload ->
                                    match decodePayload eventType payload with
                                    | Error error -> Error(ReadFault.StorageInvalid error)
                                    | Ok event -> Ok(events @ [ event ]))
                    (Ok [])

            match decoded with
            | Error fault -> Error fault
            | Ok events ->
                match pairIncomplete events with
                | Some reason -> Error(ReadFault.PairIncomplete reason)
                | None -> Ok events
