namespace Wanxiangshu.Sphinx

open System
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module Inquiry =
    type Progress =
        | Working of state: EpistemicState * request: Request
        | Complete of CanonicalAnswer

    type RootBudget =
        { Expectation: TurnExpectation
          UsedTurns: int
          ChargedWork: Set<string>
          Frontier: EventId }

    type BudgetBinding =
        | Historical
        | OwnBudget of RootBudget
        | SharedBudget of root: string

    type BudgetReport =
        { Root: string
          Expectation: TurnExpectation
          UsedTurns: int }

    type Entry =
        { Revision: int
          Head: EventId
          Progress: Progress
          Budget: BudgetBinding
          CompletionBudget: BudgetReport option }

    type Current =
        { Entries: Map<string, Entry>
          Calibration: Map<string * int, TurnCalibration> }

    let currentKey = "SphinxInquiry"

    let question entry =
        match entry.Progress with
        | Working(state, _) -> state.RootQuestion
        | Complete answer -> answer.Question

    let private ownBudget (current: Current) root =
        match Map.tryFind root current.Entries with
        | Some { Budget = OwnBudget budget } -> Some(root, budget)
        | _ -> None

    let rootBudget (current: Current) (invocationId: string) =
        match Map.tryFind invocationId current.Entries with
        | Some { Budget = OwnBudget budget } -> Some(invocationId, budget)
        | Some { Budget = SharedBudget root } -> ownBudget current root
        | _ -> None

    let budgetIsOpen (current: Current) invocationId =
        let isWorking entry =
            match entry.Progress with
            | Working _ -> true
            | Complete _ -> false

        rootBudget current invocationId
        |> Option.map (fun (root, _) -> Map.find root current.Entries |> isWorking)
        |> Option.defaultValue true

    let budgetReport current invocationId =
        match
            Map.tryFind invocationId current.Entries
            |> Option.bind (fun e -> e.CompletionBudget)
        with
        | Some report -> Some report
        | None ->
            rootBudget current invocationId
            |> Option.map (fun (root, budget) ->
                { Root = root
                  Expectation = budget.Expectation
                  UsedTurns = budget.UsedTurns })

    let workId (invocationId: string) (state: EpistemicState) =
        invocationId + ":work:" + string state.Revision

    let private rootFromData data =
        Decode.fromValue "$.data" (Decode.field "budgetRoot" (Decode.option Decode.string)) (unbox data)
        |> Result.toOption
        |> Option.flatten

    let private inheritedExpectation current root expected =
        match Map.tryFind root current.Entries, ownBudget current root with
        | Some { Progress = Working _ }, Some(_, budget) when
            expected
            |> Option.exists (fun target -> target <> budget.Expectation.ExpectedTurns)
            ->
            Error "nested expectTurns cannot replace the root budget"
        | Some { Progress = Working _ }, Some(_, budget) -> Ok budget.Expectation
        | _ -> Error "Sphinx root budget is unavailable or already terminal"

    let private chooseExpectation (current: Current) question expected root =
        match root with
        | Some rootId -> inheritedExpectation current rootId expected
        | None ->
            TurnBudget.validate (defaultArg expected TurnBudget.defaultExpected)
            |> Result.map (fun target ->
                Map.tryFind (question, target) current.Calibration
                |> Option.defaultValue TurnBudget.emptyCalibration
                |> TurnBudget.expectation target)

    let startData (current: Current) question expected root =
        chooseExpectation current question expected root
        |> Result.map (fun expectation ->
            createObj
                [ "question" ==> question
                  "expectTurns" ==> expectation.ExpectedTurns
                  "turnPrice" ==> expectation.TurnPrice
                  "calibrationSamples" ==> expectation.CalibrationSamples
                  "budgetRoot" ==> (root |> Option.toObj) ])

    let envelope (invocationId: string) (previous: Entry option) (kind: string) (data: obj) : EventEnvelope =
        let revision =
            previous
            |> Option.map (fun entry -> entry.Revision + 1)
            |> Option.defaultValue 0

        { EventId = EventId.create ("sphinx:" + invocationId + ":" + string revision)
          StreamId = EventStreamId.create ("sphinx/" + invocationId)
          EventType = SphinxEventTypes.InquiryTransition
          Parents = previous |> Option.map (fun entry -> [ entry.Head ]) |> Option.defaultValue []
          Payload =
            unbox (
                createObj
                    [ "invocation" ==> invocationId
                      "revision" ==> revision
                      "kind" ==> kind
                      "data" ==> data ]
            )
          PayloadRefs = [] }

    /// All events spending or observing a shared budget join its causal frontier.
    /// Physical session flattening does not create independent spending streams.
    let transition (current: Current) invocationId kind (data: obj) =
        let previous = Map.tryFind invocationId current.Entries
        let baseEvent = envelope invocationId previous kind data

        let budget =
            rootBudget current invocationId
            |> Option.orElseWith (fun () -> rootFromData data |> Option.bind (rootBudget current))

        let childHead =
            if kind = "charged" then
                Decode.fromValue "$.data" (Decode.field "invocation" Decode.string) (unbox data)
                |> Result.toOption
                |> Option.bind (fun child -> Map.tryFind child current.Entries)
                |> Option.map (fun child -> child.Head)
                |> Option.toList
            else
                []

        EventEnvelope.normalize
            { baseEvent with
                Parents =
                    baseEvent.Parents
                    @ (budget |> Option.map (snd >> fun b -> b.Frontier) |> Option.toList)
                    @ childHead }

    let private progress (state, result) =
        match result with
        | InquiryResult.Yield request -> Ok(Working(state, request))
        | InquiryResult.Answered answer -> Ok(Complete answer)
        | InquiryResult.Error error -> Error error

    let private historicalStart data =
        Decode.fromValue "$.data" Decode.string data
        |> Result.bind (fun question ->
            if String.IsNullOrWhiteSpace question then
                Error "question required"
            else
                let state = State.create (question.Trim())

                { state with
                    Budget =
                        { state.Budget with
                            MaxYields = 12
                            MaxCost = 12.0 } }
                |> Policy.decide
                |> progress)

    let private sharedBinding current root expectation =
        match inheritedExpectation current root (Some expectation.ExpectedTurns) with
        | Ok inherited when inherited = expectation -> Ok(SharedBudget root)
        | _ -> Error "nested Sphinx budget does not match its live root"

    let private budgetBinding current (event: EventEnvelope) expectation root =
        match root with
        | None ->
            Ok(
                OwnBudget
                    { Expectation = expectation
                      UsedTurns = 0
                      ChargedWork = Set.empty
                      Frontier = event.EventId }
            )
        | Some rootId -> sharedBinding current rootId expectation

    let private nativeStart (current: Current) (event: EventEnvelope) data =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "question" Decode.string,
                { ExpectedTurns = get.Required.Field "expectTurns" Decode.int
                  TurnPrice = get.Required.Field "turnPrice" Decode.float
                  CalibrationSamples = get.Required.Field "calibrationSamples" Decode.int },
                get.Optional.Field "budgetRoot" Decode.string)

        Decode.fromValue "$.data" decoder data
        |> Result.bind (fun (text, expectation, root) ->
            TurnBudget.validate expectation.ExpectedTurns
            |> Result.bind (fun _ ->
                if
                    String.IsNullOrWhiteSpace text
                    || expectation.TurnPrice <= 0.0
                    || Double.IsNaN expectation.TurnPrice
                    || Double.IsInfinity expectation.TurnPrice
                    || expectation.CalibrationSamples < 0
                then
                    Error "invalid native Sphinx budget or question"
                else
                    budgetBinding current event expectation root
                    |> Result.bind (fun budget ->
                        let state = State.create (text.Trim())

                        { state with
                            Budget =
                                { state.Budget with
                                    MaxYields = TurnBudget.safetyLimit
                                    Expectation = Some expectation } }
                        |> Policy.decide
                        |> progress
                        |> Result.map (fun next -> next, budget))))

    let private observationState current invocationId (state: EpistemicState) =
        match rootBudget current invocationId with
        | None -> Ok state
        | Some _ when not (budgetIsOpen current invocationId) -> Error "root budget is already terminal"
        | Some(_, budget) when Set.contains (workId invocationId state) budget.ChargedWork ->
            Ok
                { state with
                    Budget =
                        { state.Budget with
                            MaxYields = state.Budget.UsedYields + max 0 (TurnBudget.safetyLimit - budget.UsedTurns) } }
        | Some _ -> Error "observation has no charged work identity"

    let private stopped state data =
        Decode.fromValue "$.data" Decode.string data
        |> Result.bind (fun reason ->
            if String.IsNullOrWhiteSpace reason then
                Error "stop reason required"
            else
                Ok(Complete(Policy.canonicalAnswer reason state)))

    let private advance (current: Current) invocationId entry kind data =
        match entry.Progress, kind with
        | Complete _, _ -> Error "inquiry is already terminal"
        | Working(state, _), "observed" ->
            observationState current invocationId state
            |> Result.bind (fun admitted ->
                ObservationCodec.decode (box data)
                |> Result.bind (Policy.resume admitted >> progress))
        | Working(state, _), "stopped" -> stopped state data
        | Working _, _ -> Error("unknown inquiry transition: " + kind)

    let private charge current invocationId (entry: Entry) data =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "invocation" Decode.string, get.Required.Field "workId" Decode.string)

        Decode.fromValue "$.data" decoder data
        |> Result.bind (fun (childId, key) ->
            match entry.Progress, entry.Budget, Map.tryFind childId current.Entries, rootBudget current childId with
            | Working _, OwnBudget budget, Some { Progress = Working(state, _) }, Some(root, _) when
                root = invocationId && key = workId childId state
                ->
                if Set.contains key budget.ChargedWork then
                    Error "work identity already charged"
                elif budget.UsedTurns >= TurnBudget.safetyLimit then
                    Error "root work budget exhausted"
                else
                    Ok(
                        entry.Progress,
                        OwnBudget
                            { budget with
                                UsedTurns = budget.UsedTurns + 1
                                ChargedWork = Set.add key budget.ChargedWork }
                    )
            | _ -> Error "invalid root budget charge")

    let private recordCompletion current invocationId (answer: CanonicalAnswer) =
        let terminal = Map.find invocationId current.Entries
        let report = budgetReport current invocationId

        let updated =
            { current with
                Entries =
                    Map.add
                        invocationId
                        { terminal with
                            CompletionBudget = report }
                        current.Entries }

        match terminal.Budget, report with
        | OwnBudget _, Some report when answer.StopReason = "turn-price" && report.UsedTurns >= TurnBudget.minimum ->
            let key = answer.Question, report.Expectation.ExpectedTurns

            let previous =
                Map.tryFind key updated.Calibration
                |> Option.defaultValue TurnBudget.emptyCalibration

            { updated with
                Calibration =
                    Map.add key (TurnBudget.observe report.Expectation report.UsedTurns previous) updated.Calibration }
        | _ -> updated

    let private finishEvent current invocationId (event: EventEnvelope) entry =
        let updated =
            { current with
                Entries = Map.add invocationId entry current.Entries }

        let updated =
            match rootBudget updated invocationId with
            | None -> updated
            | Some(root, budget) ->
                let owner = Map.find root updated.Entries

                { updated with
                    Entries =
                        Map.add
                            root
                            { owner with
                                Budget = OwnBudget { budget with Frontier = event.EventId } }
                            updated.Entries }

        match entry.Progress with
        | Working _ -> updated
        | Complete answer -> recordCompletion updated invocationId answer

    let private start current event data =
        match Decode.fromValue "$.data" Decode.string data with
        | Ok _ -> historicalStart data |> Result.map (fun next -> next, Historical)
        | Error _ -> nativeStart current event data

    let private decoder =
        Decode.object (fun get ->
            get.Required.Field "invocation" Decode.string,
            get.Required.Field "revision" Decode.int,
            get.Required.Field "kind" Decode.string,
            get.Required.Field "data" Decode.value)

    let apply (current: Current) (event: EventEnvelope) : Result<Current, string> =
        Decode.fromValue "$" decoder event.Payload
        |> Result.bind (fun (invocationId, revision, kind, data) ->
            let previous = Map.tryFind invocationId current.Entries
            let expected = transition current invocationId kind (box data)

            if String.IsNullOrWhiteSpace invocationId then
                Error "invocation identity required"
            elif event.EventId <> expected.EventId || event.StreamId <> expected.StreamId then
                Error "inquiry event identity mismatch"
            elif
                event.Parents <> expected.Parents
                || revision
                   <> (previous |> Option.map (fun e -> e.Revision + 1) |> Option.defaultValue 0)
            then
                Error "inquiry revision conflict"
            else
                let next =
                    match previous, kind with
                    | None, "started" -> start current event data
                    | None, _ -> Error "inquiry has not started"
                    | Some _, "started" -> Error "inquiry already exists"
                    | Some entry, "charged" -> charge current invocationId entry data
                    | Some entry, _ ->
                        advance current invocationId entry kind data
                        |> Result.map (fun next -> next, entry.Budget)

                next
                |> Result.map (fun (value, budget) ->
                    finishEvent
                        current
                        invocationId
                        event
                        { Revision = revision
                          Head = event.EventId
                          Progress = value
                          Budget = budget
                          CompletionBudget = None }))

    let rule: IntegrationRule =
        { Name = currentKey
          Initial =
            box
                { Entries = Map.empty
                  Calibration = Map.empty }
          FaultScope = fun event -> EventStreamId.value event.StreamId
          Accepts = fun event -> event.EventType = SphinxEventTypes.InquiryTransition
          Integrate = fun current event -> apply (unbox<Current> current) event |> Result.map box
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }
