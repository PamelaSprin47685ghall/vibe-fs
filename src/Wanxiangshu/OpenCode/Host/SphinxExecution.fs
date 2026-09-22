namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx

module SphinxExecution =
    [<Emit("JSON.parse($0.trim().replace(/^```(?:json)?\\s*([\\s\\S]*?)\\s*```$/, '$1'))")>]
    let private parseJson (_text: string) : obj = jsNative

    [<Emit("$0 !== null && typeof $0 === 'object' && !Array.isArray($0)")>]
    let private isObject (_value: obj) : bool = jsNative

    let parseObservation text =
        try
            let raw = parseJson text

            if isObject raw then
                Ok raw
            else
                Error "Engineer observation must be a JSON object"
        with error ->
            Error("Engineer observation is not valid JSON: " + error.Message)

    let prompt question expectTurns (work: obj) =
        let expectation =
            match expectTurns with
            | None -> "Choose useful depth from the question and evidence."
            | Some turns ->
                sprintf
                    "The user expects approximately %d inquiry turns. This is guidance, NOT a limit or a quota. Continue if needed, stop when sufficient, and never invent work just to meet that number."
                    turns

        String.concat
            "\n\n"
            [ "You are a standard Engineer completing one Sphinx work item. Use your normal Engineer permissions and obey the ordinary authority/worktree rules. Sphinx is a program, not another agent or session. Complete only the supplied request and return one JSON observation as your final visible response, without surrounding commentary."
              expectation
              "work.budget is the root inquiry's shared budget. Its expectedTurns covers all work items, including nested inquiries, not this Engineer alone. Its price is already fixed by the program; do not reset or override it. usedTurns counts Engineer work items, not internal provider/tool calls."
              "Observation schemas (use ONLY the schema matching work.request.type):\nSemanticAssessmentRequest: {\"type\":\"SemanticAssessment\",\"forms\":{\"Why\":0.5,\"How\":0.5},\"facets\":{\"causal\":1},\"targets\":[],\"intents\":[]}. Forms may include Why, How, What, Who, Where, When, Which, Polar, Other; preserve uncertainty.\nGenerateCandidatesRequest: {\"type\":\"Candidates\",\"items\":[{\"method\":\"a method from request.methods\",\"question\":\"useful investigation\",\"semanticKey\":\"stable key\",\"expectedRootGain\":0.5,\"gatewayGain\":0,\"cost\":0.2}]}. Return items:[] when no useful new investigation remains. Proposals are NOT evidence.\nInvestigateRequest: {\"type\":\"Investigation\",\"actionKey\":\"exact request.action.id\",\"findings\":[{\"semanticKey\":\"finding key\",\"text\":\"finding\",\"evidenceKeys\":[\"evidence key\"]}],\"evidence\":[{\"semanticKey\":\"evidence key\",\"proposition\":\"observed fact\",\"source\":{\"id\":\"actual source or tool result\",\"kind\":\"tool\"},\"dependencyKey\":\"source dependency\"}]}. Investigate using normal tools as needed; never fabricate a source, execution result or observation.\nSynthesizeRequest: {\"type\":\"Synthesis\",\"text\":\"answer in the user's language\",\"findingKeys\":[\"existing keys from request.findingKeys\"],\"uncertainties\":[]}. Organize the known basis only; do not acquire or invent new evidence in synthesis."
              "Root question:\n" + question
              "Current work and known basis:\n" + JS.JSON.stringify work ]

/// DSL-state-combination: physical — live invocation cancellation and awaitable completion.
type private SphinxFlight =
    { Session: string
      Question: string
      ExpectedTurns: int option
      Work: Task<obj>
      Cancel: unit -> unit }

type ISphinxEngineerPort =
    abstract Invoke:
        owner: SessionId * charge: string * admitted: (SessionId -> unit) * isCancelled: (unit -> bool) ->
            Task<Result<string, string>>

    abstract Cancel: child: SessionId -> Task<unit>
    abstract LogicalOwnerOf: present: SessionId -> SessionId

/// Uses the ordinary reusable Engineer runtime. It neither creates physical
/// sessions nor defines a second tool map, authority profile or depth policy.
type SphinxExecution(store: IEventStore, engineers: ISphinxEngineerPort) =
    let inquiry = InquiryRuntime store
    let flights = Dictionary<string, SphinxFlight>()
    let tails = Dictionary<string, string * Task<unit>>()
    let workerBudgets = Dictionary<string, string>()
    // DSL-MUTABLE: resource — disposal closes admission to this plugin instance.
    let mutable disposed = false

    let attempt (work: unit -> Task<obj>) =
        task {
            try
                let! value = work ()
                return Ok value
            with error ->
                return Error error
        }

    let awaitDrain pending =
        defaultArg pending (Task.FromResult(()))

    let releaseTail sessionId invocationId =
        match tails.TryGetValue sessionId with
        | true, (tailId, _) when tailId = invocationId -> tails.Remove sessionId |> ignore
        | _ -> ()

    let settleFlight (flight: SphinxFlight) =
        task {
            try
                let! _ = flight.Work
                return None
            with error ->
                return Some error
        }

    let start (context: HostToolContext) invocationId question expectedTurns =
        let cancelled = ref false
        let child = ref None
        let drain = ref None
        let owner = SessionId.create context.SessionId

        let inheritedBudget =
            match workerBudgets.TryGetValue(SessionId.value (engineers.LogicalOwnerOf owner)) with
            | true, root -> Some root
            | _ -> None

        let budgetRoot = defaultArg inheritedBudget invocationId

        let cancel () =
            cancelled.Value <- true

            match child.Value, drain.Value with
            | Some childId, None -> drain.Value <- Some(engineers.Cancel childId)
            | _ -> ()

        let previous =
            match tails.TryGetValue context.SessionId with
            | true, (_, pending) -> pending
            | _ -> Task.FromResult(())

        let released =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        tails.[context.SessionId] <- (invocationId, released.Task)
        let detachAbort = context.AttachAbort cancel

        let observe work =
            task {
                let text = SphinxExecution.prompt question expectedTurns work

                let admitted childId =
                    // This callback runs only after this invocation wins ordinary
                    // delegate admission. Never cancel another caller's worker.
                    child.Value <- Some childId
                    workerBudgets.[SessionId.value childId] <- budgetRoot

                    if cancelled.Value then
                        cancel ()

                try
                    let! response = engineers.Invoke(owner, text, admitted, (fun () -> cancelled.Value))
                    return response |> Result.bind SphinxExecution.parseObservation
                finally
                    child.Value
                    |> Option.iter (fun childId -> workerBudgets.Remove(SessionId.value childId) |> ignore)

                    child.Value <- None
            }

        let execute () =
            task {
                try
                    do! previous

                    let! outcome =
                        attempt (fun () ->
                            inquiry.Run(
                                invocationId,
                                question,
                                observe,
                                (fun () -> cancelled.Value),
                                ?expectTurns = expectedTurns,
                                ?budgetRoot = inheritedBudget
                            ))

                    do! awaitDrain drain.Value
                    return outcome |> Result.map (fun value -> value?answer) |> Result.defaultWith raise
                finally
                    detachAbort ()
                    flights.Remove invocationId |> ignore
                    releaseTail context.SessionId invocationId
                    AsyncSupport.trySetResult released () |> ignore
            }

        let work: Task<obj> = emitJsExpr execute "Promise.resolve().then($0)"

        flights.Add(
            invocationId,
            { Session = context.SessionId
              Question = question
              ExpectedTurns = expectedTurns
              Work = work
              Cancel = cancel }
        )

        work

    member _.Run(context: HostToolContext, invocationId: string, question: string, expectTurns: int option) =
        if disposed then
            invalidOp "Sphinx executor is disposed"

        if String.IsNullOrWhiteSpace context.SessionId then
            invalidArg "sessionId" "session required"

        if String.IsNullOrWhiteSpace invocationId then
            invalidArg "invocationId" "invocation identity required"

        if String.IsNullOrWhiteSpace question then
            invalidArg "question" "question required"

        expectTurns
        |> Option.iter (fun count ->
            match TurnBudget.validate count with
            | Ok _ -> ()
            | Error error -> invalidArg "expectTurns" error)

        let question = question.Trim()

        match flights.TryGetValue invocationId with
        | true, flight when
            flight.Question <> question
            || flight.ExpectedTurns <> expectTurns
            || flight.Session <> context.SessionId
            ->
            invalidOp "Sphinx invocation identity was reused with different arguments"
        | true, flight -> flight.Work
        | _ -> start context invocationId question expectTurns

    member _.CancelSession(sessionId) =
        flights.Values
        |> Seq.toArray
        |> Array.iter (fun flight ->
            if flight.Session = sessionId then
                flight.Cancel())

    member _.DisposeAsync() : Task =
        task {
            disposed <- true
            let pending = flights.Values |> Seq.toArray

            for flight in pending do
                flight.Cancel()

            let failure = ref None

            for flight in pending do
                let! observed = settleFlight flight
                failure.Value <- failure.Value |> Option.orElse observed

            failure.Value |> Option.iter raise
            return ()
        }
