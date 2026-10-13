namespace Wanxiangshu.Mission.Lifecycle

open System
open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore

module LifecycleSurface =

    let private sha256 = HostDigest.sha256Hex

    let authorityInitial (workId: string) = LifecycleIdentity.authorityInitial sha256 workId

    let private document (events: LifecycleEvent list) =
        events |> List.map LifecycleCodec.encodeLine |> String.concat "\n" |> fun body -> body + "\n"

    let encodeOpening (kind: string) (workId: string) (root: string) (head: int64) =
        let openingKind =
            match kind with
            | "S4" -> OpeningKind.ManagerDirect
            | "spawn" -> OpeningKind.SpawnedExecution
            | _ -> OpeningKind.UserPlan

        let firstLine = if kind = "spawn" then Some LifecycleIdentity.spawnMark else None

        match LifecycleKernel.openWork sha256 openingKind firstLine workId root false (Some head) with
        | KernelDecision.Write(events, _) -> document events
        | KernelDecision.Refuse reason -> "REFUSE " + reason
        | KernelDecision.Done label -> "DONE " + label

    let private probeOf (text: string) =
        match LifecycleKernel.readRecovery text false with
        | Error(ReadFault.StorageInvalid reason) -> ("StorageInvalid", reason, false, "", "")
        | Error(ReadFault.PairIncomplete reason) -> ("PairIncomplete", reason, false, "", "")
        | Error(ReadFault.Rejected reason) -> ("Rejected", reason, false, "", "")
        | Ok(position, next) ->
            match LifecycleCodec.readDocument text with
            | Error fault ->
                match fault with
                | ReadFault.StorageInvalid reason -> ("StorageInvalid", reason, false, "", "")
                | ReadFault.PairIncomplete reason -> ("PairIncomplete", reason, false, "", "")
                | ReadFault.Rejected reason -> ("Rejected", reason, false, "", "")
            | Ok events ->
                match LifecycleFold.applyAll events with
                | Error reason -> ("Rejected", reason, false, "", "")
                | Ok work ->
                    let authorized =
                        match LifecycleFold.folded work with
                        | Ok folded -> folded.DeliveryAuthorized
                        | Error _ -> false

                    let sequence = WorkSequence.render work.Sequence |> String.concat ","
                    let names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                    let positionName =
                        match position with
                        | RecoveryPosition.PairIncomplete -> "PairIncomplete"
                        | RecoveryPosition.Rejected -> "Rejected"
                        | RecoveryPosition.Conflict -> "Conflict"
                        | RecoveryPosition.DeliveredIdempotent -> "DeliveredIdempotent"
                        | RecoveryPosition.ActiveRebound -> "ActiveRebound"
                        | RecoveryPosition.Nothing -> "Nothing"

                    let nextName =
                        match next with
                        | RecoveryNext.StopPairIncomplete -> "StopPairIncomplete"
                        | RecoveryNext.StopRejected -> "StopRejected"
                        | RecoveryNext.StopConflict -> "StopConflict"
                        | RecoveryNext.StopDelivered -> "StopDelivered"
                        | RecoveryNext.StopNothing -> "StopNothing"
                        | RecoveryNext.WaitForUserPrompt -> "WaitForUserPrompt"
                        | RecoveryNext.EnterSuccessorPrompt -> "EnterSuccessorPrompt"
                        | RecoveryNext.EnterDispatch -> "EnterDispatch"

                    (positionName, nextName, authorized, sequence, names)

    let readProbe (text: string) =
        let position, detail, authorized, sequence, names = probeOf text
        box
            {| position = position
               detail = detail
               next = detail
               authorized = authorized
               sequence = sequence
               names = names |}

    let private readFaultText (fault: ReadFault) =
        match fault with
        | ReadFault.StorageInvalid reason
        | ReadFault.PairIncomplete reason
        | ReadFault.Rejected reason -> reason

    let private workOf (text: string) =
        LifecycleCodec.readDocument text
        |> Result.mapError readFaultText
        |> Result.bind LifecycleFold.applyAll

    let reviewProbe (text: string) (incumbencyId: string) (outcome: string) =
        match workOf text, ReviewOutcome.parse outcome with
        | Error error, _ -> box {| decision = "Refuse"; detail = error |}
        | _, None -> box {| decision = "Refuse"; detail = "review outcome rejected" |}
        | Ok work, Some reviewOutcome ->
            match LifecycleKernel.review work incumbencyId reviewOutcome with
            | KernelDecision.Refuse reason -> box {| decision = "Refuse"; detail = reason |}
            | KernelDecision.Done label -> box {| decision = "Done"; detail = label |}
            | KernelDecision.Write(events, label) ->
                let folded = workOf (text + document events)
                let authorized =
                    match folded with
                    | Ok next ->
                        match LifecycleFold.folded next with
                        | Ok view -> view.DeliveryAuthorized
                        | Error _ -> false
                    | Error _ -> false

                box
                    {| decision = "Write"
                       detail = label
                       authorized = authorized
                       names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                       bytes = document events |}

    let finalPromotionProbe (text: string) =
        let rec promote (current: string) (remaining: int) =
            if remaining = 0 then
                match workOf current with
                | Error error -> box {| authorized = false; detail = error |}
                | Ok work ->
                    match LifecycleFold.folded work with
                    | Ok folded -> box {| authorized = folded.DeliveryAuthorized; detail = StageElement.render folded.Projection |}
                    | Error error -> box {| authorized = false; detail = error |}
            else
                match workOf current with
                | Error error -> box {| authorized = false; detail = error |}
                | Ok work ->
                    match work.Active with
                    | None -> box {| authorized = false; detail = "no active incumbency" |}
                    | Some active ->
                        match LifecycleKernel.review work active.Id ReviewOutcome.Promoted with
                        | KernelDecision.Write(events, _) ->
                            let reviewed = current + document events
                            match LifecycleFold.folded (match workOf reviewed with | Ok value -> value | Error _ -> work) with
                            | Ok folded when folded.DeliveryAuthorized ->
                                box {| authorized = true; detail = StageElement.render folded.Projection |}
                            | Ok _ ->
                                let reviewedWork =
                                    match workOf reviewed with
                                    | Ok value -> value
                                    | Error _ -> work

                                match
                                    LifecycleKernel.deliverOneshot
                                        sha256
                                        reviewedWork
                                        { ProviderRunId = Some "run-final"
                                          ToolCallId = Some(string remaining) }
                                        (Some 20L)
                                        None
                                        SessionLookup.None
                                        None
                                        None
                                with
                                | KernelDecision.Write(relayed, _) -> promote (reviewed + document relayed) (remaining - 1)
                                | KernelDecision.Refuse reason -> box {| authorized = false; detail = reason |}
                                | KernelDecision.Done label -> box {| authorized = false; detail = label |}
                            | Error error -> box {| authorized = false; detail = error |}
                        | KernelDecision.Refuse reason -> box {| authorized = false; detail = reason |}
                        | KernelDecision.Done label -> box {| authorized = false; detail = label |}

        promote text 3

    let private fence providerRunId toolCallId =
        { HostFence.ProviderRunId = Some providerRunId
          HostFence.ToolCallId = Some toolCallId }

    let deliverProbe (text: string) (providerRunId: string) (toolCallId: string) (head: int64) =
        match workOf text with
        | Error error -> box {| decision = "Refuse"; detail = error |}
        | Ok work ->
            let decision =
                if work.Sequence = WorkSequence.Execution then
                    LifecycleKernel.deliverExecution sha256 work (fence providerRunId toolCallId) (Some head) None
                else
                    LifecycleKernel.deliverOneshot
                        sha256
                        work
                        (fence providerRunId toolCallId)
                        (Some head)
                        None
                        SessionLookup.None
                        None
                        None

            match decision with
            | KernelDecision.Refuse reason -> box {| decision = "Refuse"; detail = reason |}
            | KernelDecision.Done label -> box {| decision = "Done"; detail = label; names = "" |}
            | KernelDecision.Write(events, label) ->
                box
                    {| decision = "Write"
                       detail = label
                       names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                       bytes = document events |}

    let replayProbe (text: string) (providerRunId: string) (toolCallId: string) =
        deliverProbe text providerRunId toolCallId 1L

    let detachedOpeningProbe (text: string) =
        match workOf text with
        | Error error -> box {| decision = "Refuse"; detail = error; names = "" |}
        | Ok work ->
            match LifecycleKernel.refuseDetachedOpening work with
            | KernelDecision.Write(events, _) ->
                box
                    {| decision = "Write"
                       detail = "opened"
                       names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat "," |}
            | KernelDecision.Refuse reason -> box {| decision = "Refuse"; detail = reason; names = "" |}
            | KernelDecision.Done label -> box {| decision = "Done"; detail = label; names = "" |}

    let commitProbe (name: string) =
        let fault = { Phase = Unchecked.defaultof<AppendPhase>; Cause = Exception name }
        let classified =
            match name with
            | "AppendDurable" -> LifecycleKernel.classifyCommit (Ok())
            | "AppendNotAttempted" ->
                LifecycleKernel.classifyCommit (
                    Error(
                        AppendError.AppendNotAttempted
                            { Requested = []
                              Prepared = None
                              Primary = fault
                              CleanupFailures = []
                              PriorRejection = None }
                    )
                )
            | "CommitUnknown" ->
                LifecycleKernel.classifyCommit (
                    Error(
                        AppendError.CommitUnknown
                            { Requested = []
                              Prepared = Unchecked.defaultof<PreparedAppend>
                              Primary = fault
                              CleanupFailures = [] }
                    )
                )
            | _ -> Error "unclassified"

        match classified with
        | Ok CommitOutcome.AppendDurable -> "AppendDurable"
        | Ok CommitOutcome.AppendNotAttempted -> "AppendNotAttempted"
        | Ok CommitOutcome.CommitUnknown -> "CommitUnknown"
        | Error _ -> "not-durable"

    let advance (workId: string) (prior: string) (expected: string) (next: string) =
        let refuse detail =
            box
                {| decision = "Refuse"
                   detail = detail
                   names = ""
                   bytes = ""
                   authority = "" |}

        let reviewed =
            let opened = encodeOpening "plan" workId "/repo" 7L
            if opened.StartsWith "REFUSE " then
                Error opened
            else
                match workOf opened with
                | Error error -> Error error
                | Ok work ->
                    match work.Active, ReviewOutcome.parse "Stayed" with
                    | None, _ -> Error "no active incumbency"
                    | _, None -> Error "review outcome rejected"
                    | Some active, Some outcome ->
                        match LifecycleKernel.review work active.Id outcome with
                        | KernelDecision.Write(events, _) -> Ok(opened + document events)
                        | KernelDecision.Refuse reason -> Error reason
                        | KernelDecision.Done label -> Error label

        match reviewed with
        | Error error -> refuse error
        | Ok text ->
            match workOf (text + prior) with
            | Error error -> refuse error
            | Ok work ->
                match LifecycleKernel.advance work expected next with
                | KernelDecision.Refuse reason ->
                    box
                        {| decision = "Refuse"
                           detail = reason
                           names = ""
                           bytes = ""
                           authority = work.Authority |}
                | KernelDecision.Done label ->
                    box
                        {| decision = "Done"
                           detail = label
                           names = ""
                           bytes = ""
                           authority = work.Authority |}
                | KernelDecision.Write(events, label) ->
                    box
                        {| decision = "Write"
                           detail = label
                           names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                           bytes = document events
                           authority = work.Authority |}

    let decodeDocument (text: string) =
        match LifecycleCodec.readDocument text with
        | Error fault ->
            box
                {| ok = false
                   names = ""
                   fault = readFaultText fault |}
        | Ok events ->
            box
                {| ok = true
                   names = events |> List.map LifecycleCodec.eventTypeOf |> String.concat ","
                   fault = "" |}

    // Test-facing seeding: commit real unified lifecycle events into the
    // workspace store the plugin already reads. No relay facts, no certificate,
    // no second vocabulary. These helpers exist so executable tests can stand up
    // an incumbency, a review, or a retirement without inventing a private store.
    let private heldStore (store: obj) : IEventStore = unbox store

    let private workOfStore (held: IEventStore) (workId: string) : LifecycleWork option =
        match held.TryCurrent "Lifecycle" with
        | None -> None
        | Some current ->
            let works = unbox<Map<string, LifecycleWork>> current
            Map.tryFind workId works

    let private commitEvents (held: IEventStore) (workId: string) (events: LifecycleEvent list) : Task<Result<unit, string>> =
        task {
            match! LifecycleKernel.commit held workId events with
            | Ok CommitOutcome.AppendDurable -> return Ok()
            | Ok CommitOutcome.AppendNotAttempted -> return Error "append was not attempted"
            | Ok CommitOutcome.CommitUnknown -> return Error "commit outcome is unknown"
            | Error reason -> return Error reason
        }

    let private openingKindOf (kind: string) =
        match kind with
        | "S4" -> OpeningKind.ManagerDirect
        | "spawn" -> OpeningKind.SpawnedExecution
        | _ -> OpeningKind.UserPlan

    let seedOpen (store: obj) (kind: string) (workId: string) (root: string) (head: int64) : Task<obj> =
        task {
            let held = heldStore store

            match workOfStore held workId with
            | Some _ -> return box {| ok = true; opened = false; error = "" |}
            | None ->
                let firstLine =
                    if kind = "spawn" then Some LifecycleIdentity.spawnMark else None

                match LifecycleKernel.openWork sha256 (openingKindOf kind) firstLine workId root false (Some head) with
                | KernelDecision.Write(events, _) ->
                    match! commitEvents held workId events with
                    | Ok() -> return box {| ok = true; opened = true; error = "" |}
                    | Error reason -> return box {| ok = false; opened = false; error = reason |}
                | KernelDecision.Refuse reason -> return box {| ok = false; opened = false; error = reason |}
                | KernelDecision.Done label -> return box {| ok = false; opened = false; error = label |}
        }

    let seedReview (store: obj) (workId: string) (outcome: string) : Task<obj> =
        task {
            let held = heldStore store

            match workOfStore held workId, ReviewOutcome.parse outcome with
            | None, _ -> return box {| ok = false; reviewed = false; error = "lifecycle work is missing" |}
            | _, None -> return box {| ok = false; reviewed = false; error = "review outcome rejected" |}
            | Some work, Some reviewOutcome ->
                match work.Active with
                | None -> return box {| ok = false; reviewed = false; error = "no active incumbency" |}
                | Some active ->
                    match LifecycleKernel.review work active.Id reviewOutcome with
                    | KernelDecision.Done label -> return box {| ok = true; reviewed = false; error = label |}
                    | KernelDecision.Refuse reason -> return box {| ok = false; reviewed = false; error = reason |}
                    | KernelDecision.Write(events, _) ->
                        match! commitEvents held workId events with
                        | Ok() -> return box {| ok = true; reviewed = true; error = "" |}
                        | Error reason -> return box {| ok = false; reviewed = false; error = reason |}
        }

    let seedDeliver
        (store: obj)
        (workId: string)
        (providerRunId: string)
        (toolCallId: string)
        (head: int64)
        : Task<obj> =
        task {
            let held = heldStore store

            match workOfStore held workId with
            | None -> return box {| ok = false; decision = "missing"; error = "lifecycle work is missing" |}
            | Some work ->
                let fence =
                    { HostFence.ProviderRunId = Some providerRunId
                      HostFence.ToolCallId = Some toolCallId }

                let decision =
                    if work.Sequence = WorkSequence.Execution then
                        LifecycleKernel.deliverExecution sha256 work fence (Some head) None
                    else
                        LifecycleKernel.deliverOneshot sha256 work fence (Some head) None SessionLookup.None None None

                match decision with
                | KernelDecision.Refuse reason -> return box {| ok = false; decision = "Refuse"; error = reason |}
                | KernelDecision.Done label -> return box {| ok = true; decision = "Done"; error = label |}
                | KernelDecision.Write(events, label) ->
                    match! commitEvents held workId events with
                    | Ok() -> return box {| ok = true; decision = label; error = "" |}
                    | Error reason -> return box {| ok = false; decision = label; error = reason |}
        }

    /// Seed the illegal fact "confirmation without review" directly. The fold
    /// accepts the event, so a test can prove the execution gate fails closed on
    /// it. The kernel's own deliver path never writes this.
    let seedConfirmation
        (store: obj)
        (workId: string)
        (providerRunId: string)
        (toolCallId: string)
        : Task<obj> =
        task {
            let held = heldStore store

            match workOfStore held workId with
            | None -> return box {| ok = false; error = "lifecycle work is missing" |}
            | Some work ->
                match work.Active with
                | None -> return box {| ok = false; error = "no active incumbency" |}
                | Some active ->
                    match!
                        commitEvents
                            held
                            workId
                            [ LifecycleEvent.ConfirmationCommitted(active.Id, providerRunId, toolCallId) ]
                    with
                    | Ok() -> return box {| ok = true; error = "" |}
                    | Error reason -> return box {| ok = false; error = reason |}
        }

    let workView (store: obj) (workId: string) =
        let held = heldStore store

        match workOfStore held workId with
        | None -> box {| found = false; sequence = ""; active = false; reviewed = ""; confirmed = false; delivered = false |}
        | Some work ->
            let reviewed =
                work.Active
                |> Option.bind (fun active -> active.Review)
                |> Option.map (fun stamp -> ReviewOutcome.render stamp.Outcome)
                |> Option.defaultValue ""

            box
                {| found = true
                   sequence = WorkSequence.render work.Sequence |> String.concat ","
                   active = work.Active.IsSome
                   reviewed = reviewed
                   confirmed =
                    work.Active
                    |> Option.bind (fun active -> active.Confirmed)
                    |> Option.isSome
                   delivered = work.CurrentDelivered |}

    let bindDevOps (store: obj) (workId: string) (devopsId: string) (target: obj) : Task<obj> =
        task {
            let held = heldStore store
            let targetText = if isNull target then None else Some(string target)

            match workOfStore held workId with
            | None -> return box {| ok = false; error = "lifecycle work is missing" |}
            | Some work ->
                match LifecycleKernel.bindDevOps work BindTrigger.Ask devopsId targetText with
                | KernelDecision.Refuse reason -> return box {| ok = false; error = reason |}
                | KernelDecision.Done _ -> return box {| ok = true; error = "" |}
                | KernelDecision.Write(events, _) ->
                    match! commitEvents held workId events with
                    | Ok() -> return box {| ok = true; error = "" |}
                    | Error reason -> return box {| ok = false; error = reason |}
        }

    let roadDevOps (store: obj) (workId: string) =
        match workOfStore (heldStore store) workId |> Option.bind (fun work -> work.DevOps) with
        | None -> null
        | Some(devopsId, target) ->
            box
                {| devopsId = devopsId
                   modelTarget = target |> Option.toObj |}
