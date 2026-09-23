namespace Wanxiangshu.Sphinx.V2.Core

open System

/// The one pure fold for every v2 inquiry.
///
/// WHAT[sphinx-v2-016]: the same event input always produces the same state. Replay
/// never contacts the network, never draws a fresh random number, and never re-runs a
/// model. That property is what lets the semantic projection claim Host-independence.
///
/// WHAT[sphinx-v2-018]: physical bindings are folded into the state because recovery
/// needs them, but the projection below excludes them, so a state hash computed over
/// the projection is not sensitive to which Host ran the work.

module Reducer =

    /// One error constructor typed to CoreError, so no call site can accidentally widen
    /// the fold's error channel to some other record with the same shape.
    let private coreError (code: string) (message: string) : CoreError =
        { Code = code; Message = message }

    let private isFinite (value: float) =
        not (Double.IsNaN value) && not (Double.IsInfinity value)

    let private goalValidate (goal: GoalSpec) : Result<GoalSpec, CoreError> =
        Goal.tryCreate goal
        |> Result.mapError (fun fault -> { Code = fault.Code; Message = fault.Message })

    let private emptyState (origin: InquiryEvent) (body: InquiryCreatedBody) : Result<InquiryState, CoreError> =
        if origin.Revision <> Revision.origin then
            Error(coreError "invalid-origin" "inquiry creation must be revision zero")
        elif origin.Parent.IsSome then
            Error(coreError "invalid-origin" "inquiry creation must not have a parent")
        else
            Budget.validateSpecs body.ResourceSpecs
            |> Result.mapError (fun fault -> { Code = fault.Code; Message = fault.Message } : CoreError)
            |> Result.bind (fun () ->
                goalValidate body.Goal
                |> Result.map (fun goal ->
                    { Id = origin.InquiryId
                      ApiVersion = "2"
                      Revision = Revision.origin
                      EventHead = Some origin.Id
                      Goal = goal
                      ResourceSpecs = body.ResourceSpecs
                      RenderReserve = body.RenderReserve
                      ConfigHash = body.ConfigHash
                      ProfileRef = body.ProfileRef
                      Graph = Map.empty
                      Edges = Map.empty
                      Certificates = Map.empty
                      Work = Map.empty
                      Reservations = Map.empty
                      SettledUsage = Map.empty
                      SettledMoneyMinor = 0L
                      Overruns = []
                      Observations = Map.empty
                      Interpretations = Map.empty
                      Rounds = Map.empty
                      Decisions = Map.empty
                      Answer = None
                      CommandReceipts = Map.empty
                      PhysicalBindings = Map.empty
                      Status = InquiryStatus.Active }))

    /// The chain check is the fold's only ordering rule. A revision that skips or
    /// repeats is not a retry to be tolerated; it is a different history.
    let private verifyChain (state: InquiryState) (event: InquiryEvent) : Result<unit, CoreError> =
        if event.InquiryId <> state.Id then
            Error(coreError "inquiry-mismatch" "event belongs to another inquiry")
        elif event.Revision <> Revision.next state.Revision then
            Error(coreError "revision-conflict" "event revision is not the next revision")
        elif event.Parent <> state.EventHead then
            Error(coreError "parent-conflict" "event parent is not the current head")
        else
            Ok()

    /// Terminal is terminal. A completed inquiry accepts later cost audits (a provider
    /// can bill after the answer) but no new business fact.
    let private admitBusinessEvent (state: InquiryState) (body: InquiryEventBody) : Result<unit, CoreError> =
        if InquiryState.isTerminal state.Status then
            match body with
            | InquiryEventBody.UsageSettled _
            | InquiryEventBody.UsageOverrunRecorded _
            | InquiryEventBody.HostTerminalRecorded _
            | InquiryEventBody.CancelRequested _ -> Ok()
            | _ -> Error(coreError "inquiry-terminal" "inquiry is terminal and accepts no new business event")
        else
            Ok()

    let private applyGoalAmended (state: InquiryState) (goal: GoalSpec) : Result<InquiryState, CoreError> =
        if goal.GoalId <> state.Goal.GoalId then
            Error(coreError "goal-mismatch" "amended goal must keep the same goal id")
        elif goal.Revision <= state.Goal.Revision then
            Error(coreError "stale-goal" "amended goal revision must advance")
        else
            goalValidate goal |> Result.map (fun amended -> { state with Goal = amended })

    let private applyRoundOpened (state: InquiryState) (body: RoundOpenedBody) : Result<InquiryState, CoreError> =
        if state.Rounds |> Map.containsKey body.RoundId then
            Error(coreError "duplicate-round" "round already exists")
        else
            Ok
                { state with
                    Rounds =
                        state.Rounds
                        |> Map.add body.RoundId
                            { RoundId = body.RoundId
                              ScopeId = body.ScopeId
                              ExpectedWork = body.ExpectedWork |> Set.ofList
                              ReceivedWork = Set.empty
                              TerminalWork = Set.empty
                              Closed = false
                              Outcome = None } }

    let private applyWorkPlanned (state: InquiryState) (specs: WorkSpec list) : Result<InquiryState, CoreError> =
        let workError (fault: WorkError) : CoreError = { Code = fault.Code; Message = fault.Message }

        let rec loop
            (work: Map<WorkId, WorkItem>)
            (remaining: WorkSpec list)
            : Result<Map<WorkId, WorkItem>, CoreError> =
            match remaining with
            | [] -> Ok work
            | spec :: rest ->
                let id = WorkId.value spec.Id

                if state.Work |> Map.containsKey spec.Id then
                    Error(coreError "duplicate-work" (sprintf "work %s already exists" id))
                elif work |> Map.containsKey spec.Id then
                    Error(coreError "duplicate-work" (sprintf "work %s is planned twice in one batch" id))
                else
                    Work.validateSpec spec
                    |> Result.mapError workError
                    |> Result.bind (fun () ->
                        loop (work |> Map.add spec.Id { Spec = spec; State = WorkState.Planned }) rest)

        loop state.Work specs
        |> Result.map (fun work -> { state with Work = work })

    let private currentWork (state: InquiryState) (workId: WorkId) : Result<WorkItem, CoreError> =
        match state.Work |> Map.tryFind workId with
        | Some item -> Ok item
        | None -> Error(coreError "unknown-work" (sprintf "work %s is not planned" (WorkId.value workId)))

    /// The fence is checked against the work's own attempt, not against a caller-supplied
    /// attempt number alone. A late result from a superseded attempt carries an older
    /// fence and must not land as the current result.
    let private fenceMatches (item: WorkItem) (spec: WorkSpec) (fromState: string) (next: WorkState) : Result<unit, CoreError> =
        if item.Spec.Attempt <> spec.Attempt then
            Error(coreError "attempt-mismatch" (sprintf "work attempt does not match: %s" fromState))
        elif item.Spec.Fence <> spec.Fence then
            Error(coreError "stale-fence" (sprintf "fence does not match the work attempt: %s" fromState))
        else
            match next with
            | WorkState.Running(_, physicalRef) when String.IsNullOrWhiteSpace physicalRef ->
                Error(coreError "missing-physical-ref" "running work requires a real physical reference")
            | _ -> Ok()

    let private legalTransition
        (state: InquiryState)
        (spec: WorkSpec)
        (fromState: string)
        (next: WorkState)
        (item: WorkItem)
        : Result<unit, CoreError> =
        let samePurpose =
            item.Spec.RoundId = spec.RoundId
            && item.Spec.PlanId = spec.PlanId
            && item.Spec.Producer = spec.Producer
            && item.Spec.Capability = spec.Capability
            && item.Spec.Input = spec.Input
            && item.Spec.OutputSchema = spec.OutputSchema
            && item.Spec.Dependencies = spec.Dependencies
            && item.Spec.ConflictKeys = spec.ConflictKeys

        let retry =
            match item.State, next with
            | WorkState.Failed _, WorkState.Ready
            | WorkState.Cancelled _, WorkState.Ready -> true
            | _ -> false

        if not samePurpose then
            Error(coreError "spec-mismatch" "work spec is immutable within its lifecycle")
        elif retry && spec.Attempt <> Attempt.next item.Spec.Attempt then
            Error(coreError "invalid-attempt" "retry must advance the attempt by exactly one")
        elif not retry && spec.Attempt <> item.Spec.Attempt then
            Error(coreError "attempt-mismatch" "work attempt does not match the planned attempt")
        else
            match item.State, next with
            | WorkState.Planned, WorkState.Ready ->
                // Dependencies must actually have succeeded. Being part of the same
                // dispatch batch is not completion.
                let satisfied =
                    spec.Dependencies
                    |> Set.forall (fun dependency ->
                        match state.Work |> Map.tryFind dependency with
                        | Some dependencyItem ->
                            match dependencyItem.State with
                            | WorkState.Succeeded _ -> true
                            | _ -> false
                        | None -> false)

                if satisfied then
                    Ok()
                else
                    Error(coreError "dependency-unsatisfied" "work dependencies are not complete")
            | WorkState.Planned, WorkState.Superseded successor
            | WorkState.Ready, WorkState.Superseded successor ->
                currentWork state successor |> Result.map (fun _ -> ())
            | WorkState.Leased _, WorkState.Running _
            | WorkState.Running _, WorkState.Running _ -> Ok()
            | WorkState.Succeeded _, WorkState.Succeeded _ ->
                Error(coreError "duplicate-observation" "an attempt already accepted an observation")
            | _ when Work.isTerminal item.State -> Error(coreError "terminal-work" "work is already terminal")
            | _ -> Ok()

    let private applyWorkTransition (state: InquiryState) (body: WorkAttemptTransitionedBody) :
        Result<InquiryState, CoreError> =
        currentWork state body.WorkId
        |> Result.bind (fun item ->
            if body.FromState <> Work.stateName item.State then
                Error(coreError "stale-work-state" (sprintf "expected %s but work is %s" body.FromState (Work.stateName item.State)))
            else
                let spec = { item.Spec with Attempt = body.Attempt; Fence = body.Fence; PhysicalRef = body.PhysicalRef }

                fenceMatches item spec body.FromState body.NextState
                |> Result.bind (fun () -> legalTransition state spec body.FromState body.NextState item)
                |> Result.map (fun () ->
                    { state with
                        Work = state.Work |> Map.add body.WorkId { Spec = spec; State = body.NextState } }))

    /// Certificate slots are patched, not replaced. Two independent slots on the same
    /// candidate merge in canonical order; two patches to the same slot with the same
    /// base conflict, and neither silently wins.
    ///
    /// The old reducer replaced the whole certificate keyed by node, which made the
    /// second writer erase the first. That is exactly the failure this shape prevents.
    /// Certificate slots are patched, not replaced. Two independent slots on the same
    /// candidate merge in canonical order; two patches to the same slot from the same
    /// base conflict, and neither silently wins.
    ///
    /// The old reducer replaced the whole certificate keyed by node, which let the
    /// second writer erase the first. That is the failure this shape prevents.
    let private slotConflict (message: string) : CoreError = coreError "certificate-conflict" message

    let private applyCertificateSlots (state: InquiryState) (patches: CertificateSlotPatch list) :
        Result<InquiryState, CoreError> =
        let certificateError (fault: CertificateError) : CoreError = { Code = fault.Code; Message = fault.Message }

        let key (patch: CertificateSlotPatch) : string =
            InquiryState.certificateKey
                patch.TargetRef
                patch.ValueSpaceId
                patch.ScopeId
                patch.SemanticsModelRef

        let slotName (patch: CertificateSlotPatch) = patch.Slot.Slot

        /// Different slots on the same candidate are independent and merge. The same
        /// slot must agree on its base revision, and an exact re-delivery is idempotent.
        let mergeSlot (existing: CertificateSlotPatch list) (patch: CertificateSlotPatch) :
            Result<CertificateSlotPatch list, CoreError> =
            let found = existing |> List.tryFind (fun candidate -> candidate.Slot.Slot = slotName patch)

            match found with
            | Some current when current.Slot.Revision <> patch.ExpectedSlotRevision ->
                Error(
                    slotConflict
                        (sprintf
                            "certificate slot %s expects revision %d but the slot is at %d"
                            (slotName patch)
                            (Revision.value patch.ExpectedSlotRevision)
                            (Revision.value current.Slot.Revision))
                )
            | Some current when current.Slot.Revision = patch.Slot.Revision && current.Slot = patch.Slot -> Ok existing
            | Some _ ->
                Error(
                    slotConflict
                        (sprintf
                            "certificate slot %s has a conflicting patch at the same base revision"
                            (slotName patch))
                )
            | None -> Ok(existing @ [ patch ])

        let rec loop
            (slots: Map<string, CertificateSlotPatch list>)
            (remaining: CertificateSlotPatch list)
            : Result<Map<string, CertificateSlotPatch list>, CoreError> =
            match remaining with
            | [] -> Ok slots
            | patch :: rest ->
                Certificate.validateSlot patch.Slot
                |> Result.mapError certificateError
                |> Result.bind (fun () ->
                    let slotKey = key patch
                    let existing = slots |> Map.tryFind slotKey |> Option.defaultValue []

                    mergeSlot existing patch
                    |> Result.bind (fun merged ->
                        loop (slots |> Map.add slotKey merged) rest))

        loop state.Certificates patches
        |> Result.map (fun certificates -> { state with Certificates = certificates })

    let private applyBudgetReserved (state: InquiryState) (body: BudgetReservedBody) : Result<InquiryState, CoreError> =
        let budgetError (fault: BudgetError) : CoreError = { Code = fault.Code; Message = fault.Message }
        let workKey =
            { WorkId = body.Reservation.WorkId
              Attempt = body.Reservation.Attempt }

        let key = InquiryState.reservationKey workKey

        if state.Reservations |> Map.containsKey key then
            Error(coreError "duplicate-reservation" (sprintf "work %s attempt is already reserved" key))
        else
            let outstanding =
                state.Reservations
                |> Map.toList
                |> List.map (fun (_, pair) -> snd pair)
                |> Budget.mergeReserved

            Budget.tryReserve state.ResourceSpecs state.SettledUsage outstanding body.Reservation
            |> Result.mapError budgetError
            |> Result.map (fun projected ->
                { state with
                    Reservations = state.Reservations |> Map.add key (workKey, projected) })

    /// Settlement books the real usage and releases only the part that can no longer be
    /// consumed. When a provider reports nothing, the reservation stays booked.
    let private applyUsageSettled (state: InquiryState) (usage: SettledUsage) : Result<InquiryState, CoreError> =
        let key = InquiryState.reservationKey { WorkId = usage.WorkId; Attempt = usage.Attempt }

        match state.Reservations |> Map.tryFind key with
        | None ->
            // No reservation (an audit arriving after release) is still bookable.
            if usage.UsageUnresolved then
                Ok { state with SettledUsage = usage.Resources }
            elif usage.Overrun then
                Ok
                    { state with
                        SettledUsage = usage.Resources
                        Overruns = state.Overruns @ [ { WorkId = usage.WorkId; Attempt = usage.Attempt; Resources = usage.Resources } ] }
            else
                Ok { state with SettledUsage = usage.Resources }
        | Some (workKey, outstanding) ->
            if usage.UsageUnresolved then
                Ok { state with SettledUsage = usage.Resources }
            else
                let booked = outstanding

                let remaining =
                    (booked, usage.Resources)
                    ||> Map.fold (fun acc resource amount ->
                        let outstanding = acc |> Map.tryFind resource |> Option.defaultValue 0.0
                        let left = outstanding - amount

                        if left <= 0.0 then
                            Map.remove resource acc
                        else
                            Map.add resource left acc)

                let overruns =
                    if usage.Overrun then
                        state.Overruns
                        @ [ { WorkId = usage.WorkId
                              Attempt = usage.Attempt
                              Resources = usage.Resources } ]
                    else
                        state.Overruns

                Ok
                    { state with
                        Reservations = state.Reservations |> Map.remove key
                        SettledUsage = usage.Resources
                        Overruns = overruns }

    let private applyResultAccepted (state: InquiryState) (body: ResultAcceptedBody) : Result<InquiryState, CoreError> =
        currentWork state body.WorkId
        |> Result.bind (fun item ->
            if item.Spec.Attempt <> body.Attempt then
                Error(coreError "attempt-mismatch" "result attempt does not match the work")
            elif item.Spec.Fence <> body.Fence then
                Error(coreError "stale-fence" "result fence does not match the work attempt")
            elif state.Observations |> Map.containsKey (ObservationId.value body.ObservationId) then
                Error(coreError "duplicate-observation" "this observation is already accepted")
            else
                match body.ResultSchema with
                | _ ->
                    let accepted =
                        { state with
                            Observations = state.Observations |> Map.add (ObservationId.value body.ObservationId) body }

                    match item.State with
                    | WorkState.Running _ ->
                        Ok
                            { accepted with
                                Work =
                                    accepted.Work
                                    |> Map.add body.WorkId
                                        { Spec = item.Spec
                                          State = WorkState.Succeeded body.Attempt } }
                    | WorkState.Succeeded _ -> Ok accepted
                    | _ ->
                        Error(
                            coreError
                                "work-not-running"
                                (sprintf "work %s is not running" (WorkId.value body.WorkId))
                        ))

    let private applyInterpretation (state: InquiryState) (body: InterpretationPendingBody) : Result<InquiryState, CoreError> =
        let id = ObservationId.value body.ObservationId

        if state.Interpretations |> Map.containsKey id then
            Ok state
        else
            Ok
                { state with
                    Interpretations =
                        state.Interpretations
                        |> Map.add id
                            { ObservationId = body.ObservationId
                              WorkId = body.WorkId
                              Attempt = body.Attempt
                              InterpretationId = None
                              PluginRef = None
                              Status = "pending"
                              Reason = None } }

    let private applyGraphPatched (state: InquiryState) (body: GraphPatchedBody) : Result<InquiryState, CoreError> =
        // The payload is the plugin's own delta; Core validates only that it is present.
        if String.IsNullOrWhiteSpace body.PluginRef then
            Error(coreError "invalid-patch" "graph patch must name its producing plugin")
        else
            Ok state

    let private applyAnswer (state: InquiryState) (body: AnswerCommittedBody) : Result<InquiryState, CoreError> =
        match state.Answer with
        | Some existing when existing = body -> Ok state
        | Some _ -> Error(coreError "answer-conflict" "inquiry answer is immutable")
        | None ->
            currentWork state body.RenderWorkId
            |> Result.map (fun _ ->
                { state with
                    Answer = Some body
                    Status = InquiryStatus.StopReached body.StopReason })

    let private applyStatus (state: InquiryState) (status: string) (reason: string) : Result<InquiryState, CoreError> =
        match status with
        | "suspended" -> Ok { state with Status = InquiryStatus.Suspended reason }
        | "failed" -> Ok { state with Status = InquiryStatus.Failed reason }
        | "cancelled" -> Ok { state with Status = InquiryStatus.Cancelled reason }
        | "cancelling" -> Ok { state with Status = InquiryStatus.Cancelling }
        | "active" ->
            if InquiryState.isTerminal state.Status then
                Error(coreError "inquiry-terminal" "a terminal inquiry cannot return to active")
            else
                Ok { state with Status = InquiryStatus.Active }
        | "input-required" -> Ok { state with Status = InquiryStatus.InputRequired reason }
        | _ -> Error(coreError "unknown-status" (sprintf "unknown inquiry status: %s" status))

    let apply (state: InquiryState option) (event: InquiryEvent) : Result<InquiryState, CoreError> =
        match state, event.Body with
        | None, InquiryEventBody.InquiryCreated body -> emptyState event body
        | None, _ -> Error(coreError "missing-inquiry" "first event must create the inquiry")
        | Some current, InquiryEventBody.InquiryCreated _ -> Error(coreError "duplicate-inquiry" "inquiry is already created")
        | Some current, body ->
            admitBusinessEvent current body
            |> Result.bind (fun () -> verifyChain current event)
            |> Result.bind (fun () ->
                let next =
                    match body with
                    | InquiryEventBody.InquiryCreated _ -> Error(coreError "duplicate-inquiry" "inquiry is already created")
                    | InquiryEventBody.GoalAmended goalBody -> applyGoalAmended current goalBody
                    | InquiryEventBody.SnapshotRegistered _ -> Ok current
                    | InquiryEventBody.DecisionScopeOpened _ -> Ok current
                    | InquiryEventBody.RoundOpened openBody -> applyRoundOpened current openBody
                    | InquiryEventBody.RoundClosed(roundId, closeOutcome) ->
                        match current.Rounds |> Map.tryFind roundId with
                        | Some record ->
                            Ok
                                { current with
                                    Rounds =
                                        current.Rounds
                                        |> Map.add roundId { record with Closed = true; Outcome = Some closeOutcome } }
                        | None -> Error(coreError "unknown-round" (sprintf "round %s is not open" (RoundId.value roundId)))
                    | InquiryEventBody.WorkPlanned specs -> applyWorkPlanned current specs
                    | InquiryEventBody.WorkAttemptTransitioned transitionBody -> applyWorkTransition current transitionBody
                    | InquiryEventBody.CertificateSlotsPatched patchesBody -> applyCertificateSlots current patchesBody.Patches
                    | InquiryEventBody.CertificateInvalidated _ -> Ok current
                    | InquiryEventBody.BudgetReserved reservedBody -> applyBudgetReserved current reservedBody
                    | InquiryEventBody.UsageSettled usageBody -> applyUsageSettled current usageBody.Usage
                    | InquiryEventBody.UsageOverrunRecorded usageBody -> applyUsageSettled current usageBody.Usage
                    | InquiryEventBody.ReservationReleased(workId, attempt) ->
                        let key = InquiryState.reservationKey { WorkId = workId; Attempt = attempt }

                        if current.Reservations |> Map.containsKey key then
                            Ok { current with Reservations = current.Reservations |> Map.remove key }
                        else
                            Ok current
                    | InquiryEventBody.DispatchRequested _
                    | InquiryEventBody.DispatchReceiptRecorded _
                    | InquiryEventBody.HostTerminalRecorded _ ->
                        // Physical facts: recovery needs them, semantics does not read them.
                        Ok current
                    | InquiryEventBody.ResultAccepted acceptedBody -> applyResultAccepted current acceptedBody
                    | InquiryEventBody.InterpretationPending pendingBody -> applyInterpretation current pendingBody
                    | InquiryEventBody.InterpretationApplied _
                    | InquiryEventBody.InterpretationFailed _ -> Ok current
                    | InquiryEventBody.GraphPatched patchedBody -> applyGraphPatched current patchedBody
                    | InquiryEventBody.DecisionRecorded _
                    | InquiryEventBody.AnswerPrepared _ -> Ok current
                    | InquiryEventBody.AnswerCommitted committedBody -> applyAnswer current committedBody
                    | InquiryEventBody.CancelRequested _ -> Ok { current with Status = InquiryStatus.Cancelling }
                    | InquiryEventBody.InquiryCancelled reason -> Ok { current with Status = InquiryStatus.Cancelled reason }
                    | InquiryEventBody.InquirySuspended reason -> Ok { current with Status = InquiryStatus.Suspended reason }
                    | InquiryEventBody.InquiryFailed reason -> Ok { current with Status = InquiryStatus.Failed reason }
                    | InquiryEventBody.InquiryStatusChanged statusBody ->
                        applyStatus current statusBody.Status statusBody.Reason

                next |> Result.map (fun value -> { value with Revision = event.Revision; EventHead = Some event.Id }))

    /// Fold a whole transition batch. The batch is the unit of durability, so a failure
    /// anywhere means no event in it is applied: the previous state is the only outcome.
    let foldBatch (events: InquiryEvent list) : Result<InquiryState, CoreError> =
        if events |> List.isEmpty then
            Error(coreError "empty-batch" "transition batch must carry at least one event")
        else
            let rec loop (state: InquiryState option) remaining =
                match remaining with
                | [] ->
                    match state with
                    | Some current -> Ok current
                    | None -> Error(coreError "empty-batch" "transition batch produced no state")
                | event :: rest ->
                    match apply state event with
                    | Ok next -> loop (Some next) rest
                    | Error fault -> Error fault

            loop None events

    let fold (events: InquiryEvent list) : Result<InquiryState, CoreError> =
        let initial : Result<InquiryState option, CoreError> = Ok None

        let step (state: Result<InquiryState option, CoreError>) (event: InquiryEvent) =
            state
            |> Result.bind (fun carried -> apply carried event |> Result.map Some)

        match List.fold step initial events with
        | Ok(Some state) -> Ok state
        | Ok None -> Error(coreError "empty-history" "inquiry has no events")
        | Error fault -> Error fault
