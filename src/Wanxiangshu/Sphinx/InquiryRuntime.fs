namespace Wanxiangshu.Sphinx

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

type InquiryRuntime(store: IEventStore) =
    let inFlight = Dictionary<string, (string * int option * string option) * Task<obj>>()
    // DSL-MUTABLE: resource — event-admission queue, never the Engineer work itself.
    let mutable writes = Task.FromResult(())

    let current () : Inquiry.Current =
        match store.TryCurrent Inquiry.currentKey with
        | Some value -> unbox value
        | None -> invalidOp "Sphinx inquiry integration rule is not installed"

    let writeEvent event =
        task {
            match! store.Append [ event ] with
            | Error error -> return invalidOp (sprintf "Sphinx event append failed: %A" error)
            | Ok receipt when not (List.isEmpty receipt.Cuts) ->
                return invalidOp "Sphinx event append was rejected by canonical integration"
            | Ok _ -> return ()
        }

    let validateAndWrite event =
        match Inquiry.apply (current ()) event with
        | Error error -> task { return invalidOp error }
        | Ok _ -> writeEvent event

    let transact (decide: Inquiry.Current -> Result<EventEnvelope option, string>) =
        let previous = writes
        let execute () =
            task {
                do! previous
                match decide (current ()) with
                | Error error -> return invalidOp error
                | Ok None -> return ()
                | Ok(Some event) -> return! validateAndWrite event
            }
        let pending: Task<unit> = emitJsExpr execute "Promise.resolve().then($0)"
        writes <- task { try do! pending with _ -> () }
        pending

    let budgetView (report: Inquiry.BudgetReport) =
        createObj
            [ "budgetRoot" ==> report.Root
              "expectedTurns" ==> report.Expectation.ExpectedTurns
              "turnPrice" ==> report.Expectation.TurnPrice
              "calibrationSamples" ==> report.Expectation.CalibrationSamples
              "usedTurns" ==> report.UsedTurns
              "safetyLimit" ==> TurnBudget.safetyLimit
              "unit" ==> "engineer-work-item" ]

    let answerView invocationId (answer: CanonicalAnswer) =
        let unresolved =
            answer.StopReason = "cancelled"
            || answer.StopReason = "budget"
            || answer.StopReason.StartsWith "invalid-observation:"
            || answer.StopReason.StartsWith "investigation-failed:"

        let rendered = Codec.answerObject answer
        Inquiry.budgetReport (current ()) invocationId
        |> Option.iter (fun report -> rendered?turnBudget <- budgetView report)
        createObj
            [ "status" ==> (if unresolved then "unresolved" else "answered")
              "answer" ==> rendered ]

    let workView invocationId state request =
        createObj
            [ "workId" ==> Inquiry.workId invocationId state
              "attempt" ==> 1
              "request" ==> Codec.requestObject request
              "budget" ==> (Inquiry.budgetReport (current ()) invocationId |> Option.map budgetView |> Option.toObj)
              "basis" ==> Codec.answerObject (Policy.canonicalAnswer "in-progress" state) ]

    let stop invocationId reason =
        transact (fun current -> Ok(Some(Inquiry.transition current invocationId "stopped" (box reason))))

    let charge invocationId state =
        task {
            let key = Inquiry.workId invocationId state
            do! transact (fun current ->
                match Inquiry.rootBudget current invocationId with
                | None -> Ok None
                | Some _ when not (Inquiry.budgetIsOpen current invocationId) -> Ok None
                | Some(_, budget) when Set.contains key budget.ChargedWork || budget.UsedTurns >= TurnBudget.safetyLimit -> Ok None
                | Some(root, _) ->
                    Ok(Some(Inquiry.transition current root "charged"
                        (createObj [ "invocation" ==> invocationId; "workId" ==> key ]))))
            return
                match Inquiry.rootBudget (current ()) invocationId with
                | None -> true
                | Some(_, budget) -> Inquiry.budgetIsOpen (current ()) invocationId && Set.contains key budget.ChargedWork
        }

    let observeWork observe work =
        task {
            try return! observe work
            with error -> return Error error.Message
        }

    let observationEvent current invocationId raw =
        let proposed = Inquiry.transition current invocationId "observed" raw
        match Inquiry.apply current proposed with
        | Ok _ -> proposed
        | Error error -> Inquiry.transition current invocationId "stopped" (box ("invalid-observation: " + error))

    let resultEvent current invocationId observation =
        match observation with
        | Error error -> Inquiry.transition current invocationId "stopped" (box ("investigation-failed: " + error))
        | Ok raw -> observationEvent current invocationId raw

    let acceptResult invocationId observation isCancelled =
        transact (fun current ->
            let event =
                if isCancelled () || not (Inquiry.budgetIsOpen current invocationId) then
                    Inquiry.transition current invocationId "stopped" (box "cancelled")
                else resultEvent current invocationId observation
            Ok(Some event))

    let runCharged invocationId state request observe isCancelled admitted =
        match admitted, isCancelled () || not (Inquiry.budgetIsOpen (current ()) invocationId) with
        | _, true -> stop invocationId "cancelled"
        | false, false -> stop invocationId "budget"
        | true, false ->
            task {
                let! observation = observeWork observe (workView invocationId state request)
                do! acceptResult invocationId observation isCancelled
            }

    let runWork invocationId state request observe isCancelled =
        task {
            if isCancelled () || not (Inquiry.budgetIsOpen (current ()) invocationId) then
                do! stop invocationId "cancelled"
            else
                let! admitted = charge invocationId state
                do! runCharged invocationId state request observe isCancelled admitted
        }

    let rec advance invocationId observe isCancelled =
        task {
            let entry = Map.find invocationId (current ()).Entries

            match entry.Progress with
            | Inquiry.Complete answer -> return answerView invocationId answer
            | Inquiry.Working(state, request) ->
                do! runWork invocationId state request observe isCancelled
                return! advance invocationId observe isCancelled
        }

    let defaultTarget invocationId root (expectation: TurnExpectation) =
        if root = invocationId then TurnBudget.defaultExpected else expectation.ExpectedTurns

    let argumentsMatch invocationId expected requestedRoot root (budget: Inquiry.RootBudget) =
        defaultArg expected (defaultTarget invocationId root budget.Expectation) = budget.Expectation.ExpectedTurns
        && not (requestedRoot |> Option.exists ((<>) root))

    let validateBudgetArguments current invocationId expected requestedRoot =
        match Inquiry.rootBudget current invocationId with
        | Some(root, budget) when not (argumentsMatch invocationId expected requestedRoot root budget) ->
            Error "Sphinx invocation identity was reused with different budget arguments (expectTurns)"
        | Some _ -> Ok None
        | None when expected.IsSome || requestedRoot.IsSome -> Error "historical inquiry cannot acquire a new expectTurns budget"
        | None -> Ok None

    let initialize (current: Inquiry.Current) invocationId question expected budgetRoot =
        match Map.tryFind invocationId current.Entries with
        | Some entry when Inquiry.question entry <> question -> Error "Sphinx invocation identity was reused for a different question"
        | Some _ -> validateBudgetArguments current invocationId expected budgetRoot
        | None ->
            Inquiry.startData current question expected budgetRoot
            |> Result.map (fun data -> Some(Inquiry.transition current invocationId "started" data))

    let run invocationId question expected budgetRoot observe isCancelled =
        task {
            do! transact (fun current -> initialize current invocationId question expected budgetRoot)

            return! advance invocationId observe isCancelled
        }

    let start invocationId arguments observe isCancelled =
        let question, expected, root = arguments
        let execute () =
            task {
                try return! run invocationId question expected root observe isCancelled
                finally inFlight.Remove invocationId |> ignore
            }
        let work: Task<obj> = emitJsExpr execute "Promise.resolve().then($0)"
        inFlight.Add(invocationId, (arguments, work))
        work

    let reuseOrStart invocationId arguments observe isCancelled =
        match inFlight.TryGetValue invocationId with
        | true, (known, _) when known <> arguments ->
            task { return raise (ArgumentException "Sphinx invocation identity was reused with different question or budget arguments") }
        | true, (_, work) -> work
        | false, _ -> start invocationId arguments observe isCancelled

    let validate invocationId question expected =
        if String.IsNullOrWhiteSpace question then
            Error "question required"
        elif String.IsNullOrWhiteSpace invocationId then
            Error "invocation identity required"
        elif expected |> Option.exists (TurnBudget.validate >> Result.isError) then
            Error(sprintf "expectTurns must be an integer from %d to %d" TurnBudget.minimum TurnBudget.maximum)
        else Ok(question.Trim())

    member _.Run
        (invocationId: string, question: string, observe: obj -> Task<Result<obj, string>>, isCancelled: unit -> bool,
         ?expectTurns: int, ?budgetRoot: string)
        : Task<obj> =
        match validate invocationId question expectTurns with
        | Error error -> task { return raise (ArgumentException error) }
        | Ok question -> reuseOrStart invocationId (question, expectTurns, budgetRoot) observe isCancelled
