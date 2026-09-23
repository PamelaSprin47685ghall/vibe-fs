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

/// What the caller may do after one advance.
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

module Driver =

    /// The pure materialization limit. A plugin loop that never settles stops here and
    /// reports what is left, rather than spinning (WHAT[sphinx-v2-027]).
    [<Literal>]
    let maxPureSteps = 128

    /// Whether any interpretation is still pending. Pending interpretation always comes
    /// first: an unanswered result cannot contribute to a decision.
    let private hasPendingInterpretation (state: InquiryState) : bool =
        state.Interpretations
        |> Map.toList
        |> List.exists (fun (_, record) -> record.Status = "pending")

    let private openRoundCount (state: InquiryState) : int =
        state.Rounds
        |> Map.toList
        |> List.filter (fun (_, record) -> not record.Closed)
        |> List.length

    let private pendingWorkCount (state: InquiryState) : int =
        state.Work
        |> Map.toList
        |> List.filter (fun (_, item) ->
            match item.State with
            | WorkState.Planned
            | WorkState.Ready
            | WorkState.Leased _
            | WorkState.Running _
            | WorkState.InputRequired _ -> true
            | _ -> false)
        |> List.length

    let private dispatchableCount (state: InquiryState) : int =
        InquiryState.readyWork state
        |> List.filter (fun item ->
            match item.State with
            | WorkState.Planned
            | WorkState.Ready -> true
            | _ -> false)
        |> List.length

    /// One advance step's decision about what to do next. This is a pure classification
    /// of the state; the caller performs only what this names.
    let classify (state: InquiryState) : AdvanceOutcome =
        let terminal () =
            match state.Status with
            | InquiryStatus.StopReached reason -> Some(AdvanceOutcome.Terminal("completed: " + reason))
            | InquiryStatus.Cancelled reason -> Some(AdvanceOutcome.Terminal("cancelled: " + reason))
            | InquiryStatus.Failed reason -> Some(AdvanceOutcome.Terminal("failed: " + reason))
            | _ -> None

        let pendingInterpretation () =
            match state.Interpretations |> Map.toList |> List.filter (fun (_, record) -> record.Status = "pending") with
            | [] -> None
            | refinable -> Some(AdvanceOutcome.RefinementPending(refinable |> List.length))

        let needsAuthorization () =
            match state.Status with
            | InquiryStatus.InputRequired authorization -> Some(AdvanceOutcome.InputRequired authorization)
            | _ -> None

        let cancelling () =
            match state.Status with
            | InquiryStatus.Cancelling -> Some(AdvanceOutcome.NoRunnalbeWork "cancelling")
            | _ -> None

        let awaitingOrIdle () =
            let awaitingPending () =
                match pendingWorkCount state with
                | count when count > 0 -> Some(AdvanceOutcome.AwaitingResults count)
                | _ -> None

            let awaitingDispatchable () =
                match dispatchableCount state with
                | count when count > 0 -> Some(AdvanceOutcome.AwaitingResults count)
                | _ -> None

            let awaitingRound () =
                match openRoundCount state with
                | 0 -> Some(AdvanceOutcome.NoRunnalbeWork "no dispatchable work")
                | count -> Some(AdvanceOutcome.AwaitingResults count)

            awaitingPending ()
            |> Option.orElseWith awaitingDispatchable
            |> Option.orElseWith awaitingRound
            |> Option.defaultValue (AdvanceOutcome.NoRunnalbeWork "unknown")

        terminal ()
        |> Option.orElseWith pendingInterpretation
        |> Option.orElseWith needsAuthorization
        |> Option.orElseWith cancelling
        |> Option.defaultValue (awaitingOrIdle ())
