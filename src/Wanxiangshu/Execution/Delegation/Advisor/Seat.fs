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
open Wanxiangshu.OpenCode
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
/// every later consult appends to that same child.
///
/// The seat registry below is PROCESS-LIVE and owns no durable fact: a restart loses every
/// seat and the next consult creates a fresh advisor child. The durable link and restart
/// adoption described in `requirements/advisor/WHAT.md` phase 2 are deliberately not part
/// of this contract.
///
/// A consult sends the view prompt and the intent as two separate managed prompts
/// (advisor-[002]: never concatenated), waits for the child's terminal, then reads the prose
/// back. A terminal that never arrives, a terminal that is not a completed run, or an empty
/// read-back refuses with "advisor did not answer": the seat never fabricates advice
/// (advisor-[004]).
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
        { CallerId: SessionId
          Gate: obj
          mutable ChildTask: Task<Result<SessionId, string>> option
          mutable CreateFailed: bool }

    /// DSL-MUTABLE: resource — process-live seats by caller session id; a restart empties it.
    let private seats = Dictionary<string, Seat>()
    let private registryGate = obj ()

    let private createSeat (callerSessionId: string) : Seat =
        let seat =
            { CallerId = SessionId.create callerSessionId
              Gate = obj ()
              ChildTask = None
              CreateFailed = false }

        seats.[callerSessionId] <- seat
        seat

    let private seatFor (callerSessionId: string) : Seat =
        lock registryGate (fun () ->
            match seats.TryGetValue callerSessionId with
            | true, seat -> seat
            | false, _ -> createSeat callerSessionId)

    let private createAdvisorChild (deps: AdvisorSeatDeps) (callerId: SessionId) : Task<Result<SessionId, string>> =
        task {
            let options: OpenCodeChildOptions =
                { Title = Some advisorAgent
                  Agent = Some advisorAgent
                  Directory = deps.Directory }

            match! deps.Sessions.CreateChildSession(callerId, options) with
            | Ok childId -> return Ok childId
            | Error reason -> return Error(sprintf "advisor seat creation failed: %s" reason)
        }

    let private startSeatChild (deps: AdvisorSeatDeps) (seat: Seat) : Task<Result<SessionId, string>> =
        task {
            let! created = createAdvisorChild deps seat.CallerId

            if Result.isError created then
                seat.CreateFailed <- true

            return created
        }

    let private resetCreateFailure (seat: Seat) : unit =
        if seat.CreateFailed then
            seat.ChildTask <- None
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

    let private awaitAdvisorProse
        (completion: TaskCompletionSource<Result<string, string>>)
        : Task<Result<string, string>> =
        task {
            let! terminalFirst = NodeTiming.raceExit (completion.Task :> Task) ConsultTimeoutMs

            if terminalFirst then
                return! completion.Task
            else
                return Error DidNotAnswer
        }

    let private runConsult
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (callerSessionId: string)
        (childId: SessionId)
        (intent: string)
        (viewPrompt: string)
        : Task<Result<string, string>> =
        task {
            let dispatcher = PromptJournalAdapter.forJournal journal
            let port = DispatchSessionPort.ofSessionPort deps.Sessions

            let issueIdentitySeed () =
                PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed
                    (AgentJournal.snapshot journal).AgentProjections
                    (SessionId.create callerSessionId)
                    deps.Participant

            let completion =
                TaskCompletionSource<Result<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let intentPhysical = ref None
            let pending = ResizeArray<TerminalOutcome>()
            let registrations = ResizeArray<IDisposable>()

            let belongsToIntent outcome =
                match outcome, intentPhysical.Value with
                | Completed result, Some physical ->
                    (AgentJournal.snapshot journal).AgentProjections.ChatExecutions.ByKey
                    |> Map.tryFind
                        { SessionId = childId
                          PhysicalUserMessageId = physical }
                    |> Option.bind (fun state -> state.startedEvidence)
                    |> Option.exists (fun evidence -> evidence.ProviderRun = result.ProviderRun)
                | _ -> false

            let onTerminal _ outcome =
                if belongsToIntent outcome then
                    AsyncSupport.trySetResult completion (settleTerminal outcome) |> ignore
                elif intentPhysical.Value.IsNone then
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

            try
                return!
                    taskResult {
                        let! _ = send viewPrompt ignore
                        let! _ = send intent (fun physical -> intentPhysical.Value <- Some physical)

                        pending |> Seq.iter (onTerminal childId)

                        return! awaitAdvisorProse completion
                    }
            finally
                registrations |> Seq.iter (fun registration -> registration.Dispose())
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
            let! created = ensureSeatChild deps (seatFor callerSessionId)

            match created with
            | Error reason -> return Error reason
            | Ok childId -> return! runConsult deps journal callerSessionId childId intent viewPrompt
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

    /// advisor-[004]: an unexpected failure is a refusal like any other — the caller surface
    /// stays `Result`-valued and no error path ever invents advice.
    let private attempt (work: unit -> Task<Result<string, string>>) : Task<Result<string, string>> =
        task {
            try
                return! work ()
            with ex ->
                return Error(sprintf "advisor consult failed: %s" ex.Message)
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
