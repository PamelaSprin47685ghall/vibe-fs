namespace Wanxiangshu.Execution.Delegation.Advisor

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Participant.ManagedAgents
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

/// DSL-class: PhysicalHandle — `Sessions` owns the advisor child and its terminal
/// subscription, `Journal` owns dispatch claims, `Directory`/
/// `Participant` are the child's physical dispatch coordinates; owner delegation.
type AdvisorSeatDeps =
    { Sessions: ISessionHostPort
      Journal: AgentJournal option
      Directory: string option
      Participant: string }

/// advisor-[001]: one caller session's dedicated advisor seat.
///
/// The first `consult` creates the seat's advisor child through
/// `ISessionHostPort.CreateChildSession`, so the physical parent is the family root
/// (HOST-015: a sub-agent's advisor is a sibling of its owner, never a grandchild), and
/// every later consult reuses that same child but opens a fresh root: a settled (or
/// voided) consult voids the child's logical run (F39-02), so the next consult's view
/// prompt is a new authority root rather than a continuation of a finished run.
///
/// The seat registry below is PROCESS-LIVE and owns no durable fact: a restart loses every
/// seat and the next consult creates a fresh advisor child. The durable link and restart
/// adoption described in `requirements/advisor/WHAT.md` phase 2 are deliberately not part
/// of this contract.
///
/// A consult sends the view prompt and the intent as two separate managed prompts
/// (advisor-[002]: never concatenated), waits for the child's terminal, then reads the prose
/// back. Each prompt of a consult is its own authority run: the view's run is closed as soon
/// as its own terminal arrives, so the intent prompt roots freshly and owns a committed
/// model-routing lease of its own (F39-02). A terminal that never arrives, a terminal that is
/// not a completed run, or an empty read-back refuses with "advisor did not answer": the seat
/// never fabricates advice (advisor-[004]).
[<RequireQualifiedAccess>]
module AdvisorSeat =

    /// Stable refusal: the advisor produced no prose for this consult.
    [<Literal>]
    let private DidNotAnswer = "advisor did not answer"

    /// Hard cap on one consult. A terminal that never arrives must not hang the caller's
    /// turn: the seat refuses, and the next consult reuses the same child.
    [<Literal>]
    let private ConsultTimeoutMs = 120_000


    /// AGENT-001: the managed agent this seat's child runs as.
    let private advisorAgent = ManagedAgent.nameOf Role.Advisor

    /// DSL-MUTABLE: single-flight — memoized advisor child create task for one caller session.
    /// DSL-MUTABLE: resource — create-failure latch, cleared by the next consult.
    type private Seat =
        { Journal: AgentJournal
          CallerId: SessionId
          Gate: obj
          mutable ChildTask: Task<Result<SessionId, string>> option
          mutable ChildId: SessionId option
          mutable CreateFailed: bool }

    /// DSL-MUTABLE: resource — caller seats remain isolated by actual journal identity.
    let private seats = Dictionary<string, Seat list>()
    let private registryGate = obj ()

    let private createSeat (journal: AgentJournal) (callerSessionId: string) : Seat =
        let seat =
            { Journal = journal
              CallerId = SessionId.create callerSessionId
              Gate = obj ()
              ChildTask = None
              ChildId = None
              CreateFailed = false }

        let existing =
            match seats.TryGetValue callerSessionId with
            | true, entries -> entries
            | false, _ -> []

        seats.[callerSessionId] <- seat :: existing
        seat

    let private seatFor (journal: AgentJournal) (callerSessionId: string) : Seat =
        lock registryGate (fun () ->
            let existing =
                match seats.TryGetValue callerSessionId with
                | true, entries -> entries |> List.tryFind (fun seat -> obj.ReferenceEquals(seat.Journal, journal))
                | false, _ -> None

            existing |> Option.defaultWith (fun () -> createSeat journal callerSessionId))

    let private createAdvisorChild (deps: AdvisorSeatDeps) (callerId: SessionId) : Task<Result<SessionId, string>> =
        task {
            let options: ChildSessionOptions =
                { Title = Some advisorAgent
                  Agent = Some advisorAgent
                  Directory = deps.Directory }

            match! deps.Sessions.CreateChildSession(callerId, options) with
            | Ok childId -> return Ok childId
            | Error reason -> return Error(sprintf "advisor seat creation failed: %s" reason)
        }

    let private startSeatChild (deps: AdvisorSeatDeps) (seat: Seat) : Task<Result<SessionId, string>> =
        task {
            try
                let! created = createAdvisorChild deps seat.CallerId

                lock seat.Gate (fun () ->
                    seat.ChildId <- Result.toOption created
                    seat.CreateFailed <- Result.isError created)

                return created
            with error ->
                lock seat.Gate (fun () ->
                    seat.ChildId <- None
                    seat.CreateFailed <- true)

                return raise error
        }

    let private resetCreateFailure (seat: Seat) : unit =
        if seat.CreateFailed then
            seat.ChildTask <- None
            seat.ChildId <- None
            seat.CreateFailed <- false

    let private ensureSeatChild (deps: AdvisorSeatDeps) (seat: Seat) : Task<Result<SessionId, string>> =
        lock seat.Gate (fun () ->
            // A failed create must never become a permanent poisoned single-flight:
            // the next consult re-observes the Host and tries again.
            resetCreateFailure seat

            match seat.ChildTask with
            | Some pending -> pending
            | None ->
                let pending = startSeatChild deps seat
                seat.ChildTask <- Some pending
                pending)

    let tryFindOwner (journal: AgentJournal) (childId: SessionId) : SessionId option =
        lock registryGate (fun () ->
            let owners =
                seats.Values
                |> Seq.collect id
                |> Seq.filter (fun seat -> obj.ReferenceEquals(seat.Journal, journal))
                |> Seq.choose (fun seat ->
                    lock seat.Gate (fun () ->
                        match seat.ChildTask, seat.ChildId, seat.CreateFailed with
                        | Some _, Some created, false when created = childId && seat.CallerId <> childId ->
                            Some seat.CallerId
                        | _ -> None))
                |> Seq.toList

            match owners with
            | [] -> None
            | [ owner ] -> Some owner
            | _ -> invalidOp "Advisor child has ambiguous live seat ownership")

    let private nonBlank (text: string) : string option =
        if String.IsNullOrWhiteSpace text then None else Some text

    /// A terminal that is not a completed run produced no advice: refusing here keeps a
    /// failed consult from reading an earlier consult's prose back as this consult's answer.
    let private settleTerminal (outcome: TerminalOutcome) : Result<string, string> =
        match outcome with
        | Completed result when result.Role = Role.Advisor ->
            result.TurnFormalText
            |> nonBlank
            |> Option.map Ok
            |> Option.defaultValue (Error DidNotAnswer)
        | Completed _
        | Aborted _
        | Failed _ -> Error DidNotAnswer

    let private awaitPromptTerminal
        (completion: TaskCompletionSource<Result<'answer, string>>)
        : Task<Result<'answer, string>> =
        task {
            let! terminalFirst = NodeTiming.raceExit (completion.Task :> Task) ConsultTimeoutMs

            if terminalFirst then
                return! completion.Task
            else
                return Error DidNotAnswer
        }

    /// F39-02: close the advisor child's logical run once this consult is over.
    ///
    /// The child keeps its identity, but its logical run is finished: a follow-up
    /// consult must open a fresh root instead of appending to a completed run
    /// (which has no committed model-routing lease and would fail closed with
    /// EMR-010). The durable `ChildRunVoided` fact is the existing termination
    /// evidence; the dispatcher reads the durable projection, so the close has to
    /// be durable, not process-local.
    let private voidChildRun
        (journal: AgentJournal)
        (callerSessionId: string)
        (childId: SessionId)
        : Task<Result<unit, string>> =
        task {
            let active = (PromptJournalAdapter.create journal).ProjectionFor childId

            match active.ActiveLogicalRun with
            | None -> return Ok()
            | Some _ ->
                let fact =
                    Wanxiangshu.Execution.Delegation.ExecutionFact.ChildRunVoided
                        {| ParentSessionId = SessionId.create callerSessionId
                           ChildSessionId = childId |}

                let! appended = AgentJournal.appendAgent (StreamId.Session childId) None fact journal

                return
                    appended
                    |> Result.map ignore
                    |> Result.mapError (fun failure -> sprintf "advisor consult could not close its run: %A" failure)
        }

    let private runPrompt
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (callerSessionId: string)
        (childId: SessionId)
        (prompt: string)
        (settle: TerminalOutcome -> Result<'answer, string>)
        : Task<Result<'answer, string>> =
        task {
            let dispatcher =
                PromptJournalAdapter.createWithOwnership (fun _ -> None) (tryFindOwner journal) journal
                |> PromptDispatcher.forPrompts

            let port = DispatchSessionPort.ofSessionPort deps.Sessions

            let issueIdentitySeed () =
                PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed
                    (AgentJournal.snapshot journal).AgentProjections
                    (SessionId.create callerSessionId)
                    deps.Participant

            let completion =
                TaskCompletionSource<Result<'answer, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            // DSL-MUTABLE: subscription — bind terminal observations to this prompt's physical receipt.
            let acceptedPhysical = ref None
            let pending = ResizeArray<TerminalOutcome>()
            let registrations = ResizeArray<IDisposable>()

            let belongsToPrompt outcome =
                match outcome, acceptedPhysical.Value with
                | Completed result, Some physical ->
                    (AgentJournal.snapshot journal).AgentProjections.ChatExecutions.ByKey
                    |> Map.tryFind
                        { SessionId = childId
                          PhysicalUserMessageId = physical }
                    |> Option.bind (fun state -> state.startedEvidence)
                    |> Option.exists (fun evidence -> evidence.ProviderRun = result.ProviderRun)
                | _ -> false

            let stopBelongsToPrompt (stop: TerminalStop) =
                PromptAuthorityProjectionQueries.activeProfile childId (AgentJournal.snapshot journal).AgentProjections
                |> Option.exists (fun profile -> TerminalStop.belongsTo profile.AuthorityRootUserMessageId stop)

            let settleAcceptedTerminal outcome =
                AsyncSupport.trySetResult completion (settle outcome) |> ignore

            let onTerminal _ outcome =
                let accepted =
                    match outcome with
                    | Completed _ -> belongsToPrompt outcome
                    | Aborted stop
                    | Failed stop -> acceptedPhysical.Value.IsSome && stopBelongsToPrompt stop

                if accepted then
                    settleAcceptedTerminal outcome
                elif acceptedPhysical.Value.IsNone then
                    pending.Add outcome

            use _subscription = deps.Sessions.SubscribeFutureTerminal(childId, onTerminal)

            let send text notify =
                let observer =
                    Some
                        { Notify = notify
                          AttachDisposable = registrations.Add }

                match
                    PromptAuthorityProjectionQueries.activeProfile
                        childId
                        (AgentJournal.snapshot journal).AgentProjections
                with
                | Some profile ->
                    dispatcher.SendContinuation
                        port
                        childId
                        text
                        PromptAuthority.ContinuationKind.ManagedDelegationAssignment
                        profile
                        deps.Directory
                        PromptDispatcher.AwaitMode.Await
                        observer
                | None ->
                    match issueIdentitySeed () with
                    | Error error -> Task.FromResult(Error error)
                    | Ok identity ->
                        dispatcher.SendAgentOwnerRoot
                            port
                            childId
                            text
                            identity
                            deps.Directory
                            PromptDispatcher.AwaitMode.Await
                            observer

            let boundedSend text : Task<Result<PromptKey, string>> =
                task {
                    let sending = send text (fun physical -> acceptedPhysical.Value <- Some physical)
                    let! admitted = NodeTiming.raceExit (sending :> Task) ConsultTimeoutMs

                    if admitted then
                        let! sent = sending
                        pending |> Seq.toList |> List.iter (onTerminal childId)
                        return sent
                    else
                        return Error DidNotAnswer
                }

            try
                return!
                    taskResult {
                        let! _ = boundedSend prompt
                        return! awaitPromptTerminal completion
                    }
            finally
                registrations |> Seq.iter (fun registration -> registration.Dispose())
        }

    let private runConsult
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (callerSessionId: string)
        (childId: SessionId)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        taskResult {
            let viewTerminal outcome =
                match outcome with
                | Completed _ -> Ok()
                | _ -> Error DidNotAnswer

            let! _ = runPrompt deps journal callerSessionId childId viewPrompt viewTerminal
            // F39-02: the view's real terminal closes its run before the intent roots afresh.
            let! _ = voidChildRun journal callerSessionId childId
            return! runPrompt deps journal callerSessionId childId intent settleTerminal
        }

    /// advisor-[004]: an unexpected failure is a refusal like any other — the caller surface
    /// stays `Result`-valued and no error path ever invents advice.
    let private attempt (work: unit -> Task<Result<string, string>>) : Task<Result<string, string>> =
        task {
            try
                return! work ()
            with ex ->
                return Error(sprintf "advisor consult failed: %s" ex.Message)
        }


    /// One settle point for every consult outcome: a Completed answer, an aborted or
    /// failed terminal, a timeout, a refused view, a send failure and an unexpected
    /// exception all leave the child's logical run closed, so no unfinishable run
    /// stays open and the next consult roots freshly.
    let private runConsultAndVoid
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (callerSessionId: string)
        (childId: SessionId)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        task {
            let! outcome = attempt (fun () -> runConsult deps journal callerSessionId childId intent viewPrompt)

            let! closed = voidChildRun journal callerSessionId childId

            match closed with
            | Ok() -> return outcome
            | Error reason -> return Error reason
        }


    let private requireJournal (deps: AdvisorSeatDeps) : Result<AgentJournal, string> =
        match deps.Journal with
        | Some journal -> Ok journal
        | None -> Error "No journal: an advisor consult cannot be claimed"

    let private openSeat
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (callerSessionId: string)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        task {
            let! created = ensureSeatChild deps (seatFor journal callerSessionId)

            match created with
            | Error reason -> return Error reason
            | Ok childId -> return! runConsultAndVoid deps journal callerSessionId childId intent viewPrompt
        }

    let private consultCore
        (deps: AdvisorSeatDeps)
        (callerSessionId: string)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        task {
            match requireJournal deps with
            | Error reason -> return Error reason
            | Ok journal -> return! openSeat deps journal callerSessionId intent viewPrompt
        }


    let consult
        (deps: AdvisorSeatDeps)
        (callerSessionId: string)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        attempt (fun () -> consultCore deps callerSessionId intent viewPrompt)

    let clear (callerSessionId: string) : unit =
        lock registryGate (fun () -> seats.Remove callerSessionId |> ignore)

    let clearAll () : unit =
        lock registryGate (fun () -> seats.Clear())
