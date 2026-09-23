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

    /// The pure materialization limit. A plugin loop that never settles stops here and
    /// reports what is left, rather than spinning (WHAT[sphinx-v2-027]).
    let maxPureSteps = 128

    let private hasPendingInterpretation (state: InquiryState) : bool =
        state.Interpretations
        |> Map.toList
        |> List.exists (fun (_, record) -> record.Status = "pending")

    let private pendingInterpretationCount (state: InquiryState) : int =
        state.Interpretations
        |> Map.toList
        |> List.filter (fun (_, record) -> record.Status = "pending")
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
              Hash = "empty" }
          CanonicalPayload = "{}" }

    let private interpretationEvents (state: InquiryState) : InquiryEventBody list =
        state.Interpretations
        |> Map.toList
        |> List.filter (fun (_, record) -> record.Status = "pending")
        |> List.map (fun (key, record) ->
            InquiryEventBody.InterpretationApplied
                { ObservationId = record.ObservationId
                  InterpretationId = key
                  PluginRef = record.PluginRef |> Option.defaultValue "unbound"
                  Delta = record |> fun _ -> emptyEnvelope () })

    /// The state carries typed records, so an interpretation's typed payload is
    /// reconstructed from the record rather than re-read from bytes.
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
                  PublicEnvelope = emptyEnvelope ()
                  PrivateTicket = emptyEnvelope () })

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

    /// One advance: interpret what is pending, close what is finished, reserve and
    /// dispatch what is ready. Each stage appends to the same batch, so the transition
    /// is atomic.
    let private advanceActive (state: InquiryState) : AdvancePlan =
        let interpretations = interpretationEvents state
        let closings = closeCompletedRounds state

        let refinable = hasPendingInterpretation state

        let ready = dispatchable state

        let events =
            interpretations
            @ closings
            @ (if List.isEmpty interpretations && List.isEmpty closings then
                   reservationEvents state (ready |> List.map (fun item -> item.Spec))
                   @ leaseEvents ready
                   @ dispatchEvents state ready
               else
                   [])

        let outcome () =
            let pending = pendingWorkCount state

            let refinableOutcome () =
                AdvanceOutcome.RefinementPending(pendingInterpretationCount state)

            let awaitingOutcome () =
                AdvanceOutcome.AwaitingResults(max pending ready.Length)

            let idleOutcome () =
                match awaitingRoundNote state with
                | Some _ -> AdvanceOutcome.AwaitingResults(openRounds state |> List.length)
                | None -> AdvanceOutcome.NoRunnalbeWork "no dispatchable work"

            match hasPendingInterpretation state, pending > 0, List.isEmpty ready with
            | true, _, _ -> refinableOutcome ()
            | false, true, _ -> awaitingOutcome ()
            | false, false, false -> awaitingOutcome ()
            | false, false, true -> idleOutcome ()

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
