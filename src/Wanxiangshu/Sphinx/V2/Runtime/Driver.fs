namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// The advance loop.
///
/// WHAT[sphinx-v2-016]: one Runtime drives one inquiry state. MCP, OpenCode and the JS
/// surface all call `advance`; none of them decides what comes next. This is the single
/// place where "interpret, refine, close the round, decide, dispatch" is sequenced.
///
/// WHAT[sphinx-v2-011]: intent precedes effect. A transition batch is persisted before
/// any Host or provider call is made, and an append failure produces no dispatch.

[<RequireQualifiedAccess>]
type AdvanceOutcome =
    /// Work is dispatched and awaiting results.
    | AwaitingResults of pendingCount: int
    /// Waiting on a user decision the program cannot make.
    | InputRequired of authorization: string
    /// Nothing runnable; the reason says why.
    | NoRunnalbeWork of reason: string
    /// The inquiry reached a terminal state.
    | Terminal of status: string
    /// The pure step limit was hit with work still refining.
    | RefinementPending of remaining: int

/// The events one advance proposes, plus what the caller should do next. Producing an
/// event list is the whole job; the caller persists it and only then acts.
type AdvancePlan =
    { Events: InquiryEventBody list
      Outcome: AdvanceOutcome }

module Driver =

    let private hasPendingInterpretation (state: InquiryState) : bool =
        state.Interpretations
        |> Map.toList
        |> List.exists (fun (_, record) -> record.Outcome.IsNone)

    let private pendingInterpretationCount (state: InquiryState) : int =
        state.Interpretations
        |> Map.toList
        |> List.filter (fun (_, record) -> record.Outcome.IsNone)
        |> List.length

    let private openRounds (state: InquiryState) : RoundRecord list =
        state.Rounds
        |> Map.toList
        |> List.map snd
        |> List.filter (fun record -> not record.Closed)

    let private pendingWorkCount (state: InquiryState) : int =
        state.Work
        |> Map.toList
        |> List.map snd
        |> List.filter (fun item ->
            match item.State with
            | WorkState.Planned
            | WorkState.Ready
            | WorkState.Leased _
            | WorkState.Running _
            | WorkState.InputRequired _ -> true
            | _ -> false)
        |> List.length

    /// Work whose dependencies have actually succeeded. Being in the same dispatch
    /// batch does not count (WHAT[sphinx-v2-004]).
    let private dispatchable (state: InquiryState) : WorkItem list = InquiryState.readyWork state

    let private terminalOutcome (state: InquiryState) : AdvanceOutcome option =
        match state.Status with
        | InquiryStatus.StopReached reason -> Some(AdvanceOutcome.Terminal("completed: " + reason))
        | InquiryStatus.Cancelled reason -> Some(AdvanceOutcome.Terminal("cancelled: " + reason))
        | InquiryStatus.Failed reason -> Some(AdvanceOutcome.Terminal("failed: " + reason))
        | _ -> None

    let private awaitingRoundNote (state: InquiryState) : string option =
        match openRounds state with
        | [] -> None
        | rounds -> Some(sprintf "%d round(s) still expect results" rounds.Length)

    /// Closing a round whose every member has reached a terminal state is a pure fact.
    /// Closing it early is not: the round record is the dataset boundary.
    let private closeCompletedRounds (state: InquiryState) : InquiryEventBody list =
        openRounds state
        |> List.filter (fun record ->
            record.ExpectedWork
            |> Set.forall (fun workId ->
                match state.Work |> Map.tryFind workId with
                | Some item -> Work.isTerminal item.State
                | None -> false))
        |> List.map (fun record -> InquiryEventBody.RoundClosed(record.RoundId, "all-members-terminal"))

    let private emptyEnvelope () : JsonEnvelope =
        { Schema =
            { Id = "sphinx.v2/empty@1"
              Hash = "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a" }
          CanonicalPayload = "{}" }

    let private reservationEvents (state: InquiryState) (specs: WorkSpec list) : InquiryEventBody list =
        specs
        |> List.map (fun spec ->
            InquiryEventBody.BudgetReserved
                { Reservation =
                    { WorkId = spec.Id
                      Attempt = spec.Attempt
                      Resources = Work.reserved spec
                      MoneyMinor = None }
                  RenderReserve = state.RenderReserve })

    let private dispatchEvents (state: InquiryState) (items: WorkItem list) : InquiryEventBody list =
        items
        |> List.map (fun item ->
            let spec = item.Spec

            InquiryEventBody.DispatchRequested
                { Work = spec
                  DispatchIntentId = Recovery.intentKey state.Id spec.Id spec.Attempt
                  PublicEnvelope = spec.Input |> Option.defaultWith emptyEnvelope
                  PrivateTicket = emptyEnvelope () })

    /// A planned item leaves `Planned` only when its dependencies have actually
    /// succeeded, so the promotion carries the real precondition.
    let private promotionEvents (items: WorkItem list) : InquiryEventBody list =
        items
        |> List.filter (fun item ->
            match item.State with
            | WorkState.Planned -> true
            | _ -> false)
        |> List.map (fun item ->
            InquiryEventBody.WorkAttemptTransitioned
                { WorkId = item.Spec.Id
                  Attempt = item.Spec.Attempt
                  Fence = item.Spec.Fence
                  FromState = "Planned"
                  NextState = WorkState.Ready
                  PhysicalRef = None })

    let private leaseEvents (items: WorkItem list) : InquiryEventBody list =
        items
        |> List.map (fun item ->
            InquiryEventBody.WorkAttemptTransitioned
                { WorkId = item.Spec.Id
                  Attempt = item.Spec.Attempt
                  Fence = item.Spec.Fence
                  FromState = "Ready"
                  NextState = WorkState.Leased item.Spec.Fence
                  PhysicalRef = None })

    /// The events that make one item real: its promotion, its own reservation, the
    /// lease and the dispatch intent. The reservation comes first because the
    /// dispatch is refused without it, and the lease comes before the intent because
    /// the intent names the leased attempt.
    let private dispatchEventsOf (state: InquiryState) (item: WorkItem) : InquiryEventBody list =
        promotionEvents [ item ]
        @ reservationEvents state [ item.Spec ]
        @ leaseEvents [ item ]
        @ dispatchEvents state [ item ]

    /// Work that cannot run because a dependency failed. The blockage is a fact the
    /// caller can read; it is never reported as an idle inquiry.
    let private blockedBy (state: InquiryState) : string option =
        let failedDependency (dependency: WorkId) =
            state.Work
            |> Map.tryFind dependency
            |> Option.bind (fun item ->
                match item.State with
                | WorkState.Failed _ -> Some(WorkId.value dependency)
                | _ -> None)

        state.Work
        |> Map.toList
        |> List.map snd
        |> List.tryPick (fun item ->
            match item.State with
            | WorkState.Planned ->
                item.Spec.Dependencies
                |> Set.toList
                |> List.tryPick failedDependency
                |> Option.map (fun name -> sprintf "blocked by failed dependency %s" name)
            | _ -> None)

    /// Observe supplies interpretation facts. Until then, only genuine round closings
    /// can advance; paid work must wait for the pending interpretation.
    let private advanceActive (state: InquiryState) : AdvancePlan =
        let closings = closeCompletedRounds state

        let refinable = hasPendingInterpretation state

        let ready = dispatchable state

        let cancelling =
            match state.Status with
            | InquiryStatus.Cancelling -> true
            | _ -> false

        let events =
            closings
            @ (if not refinable && not cancelling && List.isEmpty closings then
                   ready |> List.map (dispatchEventsOf state) |> List.concat
               else
                   [])

        let outcome () =
            let pending = pendingWorkCount state

            let actuallyAwaiting =
                state.Work
                |> Map.toList
                |> List.map snd
                |> List.exists (fun item ->
                    match item.State with
                    | WorkState.Leased _
                    | WorkState.Running _
                    | WorkState.InputRequired _ -> true
                    | _ -> false)

            let refinableOutcome () =
                AdvanceOutcome.RefinementPending(pendingInterpretationCount state)

            let awaitingOutcome () = AdvanceOutcome.AwaitingResults(pending)

            let idleOutcome () =
                match blockedBy state, awaitingRoundNote state with
                | Some reason, _ -> AdvanceOutcome.NoRunnalbeWork reason
                | None, Some _ -> AdvanceOutcome.AwaitingResults(openRounds state |> List.length)
                | None, None -> AdvanceOutcome.NoRunnalbeWork "no dispatchable work"

            let blockedOrAwaiting () =
                match blockedBy state, actuallyAwaiting with
                | Some reason, false -> AdvanceOutcome.NoRunnalbeWork reason
                | _ when pending > 0 -> awaitingOutcome ()
                | _ -> idleOutcome ()

            let dispatchOutcome () =
                match refinable, List.isEmpty ready with
                | true, _ -> refinableOutcome ()
                | false, false -> awaitingOutcome ()
                | false, true -> blockedOrAwaiting ()

            match cancelling with
            | true -> AdvanceOutcome.NoRunnalbeWork "cancellation requested; no new work is dispatched"
            | false -> dispatchOutcome ()

        let chosenOutcome = outcome ()

        { Events = events
          Outcome = chosenOutcome }

    let advance (state: InquiryState) : AdvancePlan =
        let body =
            terminalOutcome state
            |> Option.map (fun outcome -> { Events = []; Outcome = outcome })
            |> Option.defaultWith (fun () -> advanceActive state)

        body

    /// Pure classification for callers that only want to know the state. No events.
    let classify (state: InquiryState) : AdvanceOutcome = (advance state).Outcome
