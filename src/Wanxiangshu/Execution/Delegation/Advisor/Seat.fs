namespace Wanxiangshu.Execution.Delegation.Advisor

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process

/// DSL-class: PhysicalHandle — `Sessions` owns the advisor child and its terminal
/// subscription, `Journal`/`Snapshot` are the read-back capabilities, `Directory`/
/// `Participant` are the child's physical dispatch coordinates; owner delegation.
type AdvisorSeatDeps =
    { Sessions: ISessionHostPort
      Snapshot: ISessionSnapshotPort option
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

    /// advisor-[030] consult fence: the latest terminal identity a consult
    /// observed. A consult only accepts the terminal of the intent prompt it
    /// sent, never an older Completed that was already live when the consult
    /// started.
    type private ConsultFence =
        { ProviderRun: ProviderRunIdentity
          ConsultId: string }

    /// DSL-MUTABLE: resource — child session id → fence of the consult whose
    /// intent prompt most recently reached the child. Process-live; the seat
    /// registry below has the same lifetime.
    let private consultFences = Dictionary<string, ConsultFence>()

    let private recordFence (childId: SessionId) (providerRun: ProviderRunIdentity) (consultId: string) : unit =
        consultFences.[SessionId.value childId] <-
            { ProviderRun = providerRun
              ConsultId = consultId }

    /// The provider run a new consult must outdate. It is the previous
    /// consult's terminal run, so an older Completed — including a sticky
    /// one replayed at subscribe time — can never answer the new consult.
    let private priorRun (childId: SessionId) : ProviderRunIdentity option =
        match consultFences.TryGetValue(SessionId.value childId) with
        | true, fence -> Some fence.ProviderRun
        | false, _ -> None

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

    let private textOfParts (parts: MessagePart array) : string =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text -> Some text
                | _ -> None)
            |> String.concat ""

    let private assistantProse (message: SessionMessage) : string option =
        if String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) then
            textOfParts message.Parts |> nonBlank
        else
            None

    /// advisor-[030]: prose read-back after the fenced terminal. Only this
    /// consult's own terminal has been processed when this runs, so the last
    /// non-blank assistant message of the child is this consult's answer.
    let private latestAssistantProse (messages: SessionMessage list) : string option =
        messages |> List.rev |> List.tryPick assistantProse

    let private snapshotProseOf (snapshot: ISessionSnapshotPort) (childId: SessionId) : Task<string option> =
        task {
            match! snapshot.GetMessages childId with
            | Ok messages -> return latestAssistantProse messages
            | Error _ -> return None
        }

    let private snapshotProse (deps: AdvisorSeatDeps) (childId: SessionId) : Task<string option> =
        match deps.Snapshot with
        | None -> Task.FromResult None
        | Some snapshot -> snapshotProseOf snapshot childId

    /// advisor-[030]: the read-back happens strictly after the fenced
    /// terminal settled — the child's delivered view first, the Host
    /// snapshot as its fallback. Both are empty for an advisor that never
    /// spoke, and an empty answer is a refusal. Because only this consult's
    /// terminal has been processed, the last non-blank assistant message is
    /// this consult's answer, never an older consult's.
    let private readProse (deps: AdvisorSeatDeps) (childId: SessionId) : Task<string option> =
        task {
            let delivered =
                Wanxiangshu.Repository.Programming.Js.JsDeliveredValues.tryPeekRendered (SessionId.value childId)
                |> Option.bind nonBlank

            match delivered with
            | Some prose -> return Some prose
            | None -> return! snapshotProse deps childId
        }

    let private readAdvice (deps: AdvisorSeatDeps) (childId: SessionId) : Task<Result<string, string>> =
        task {
            let! prose = readProse deps childId

            match prose with
            | Some advice -> return Ok advice
            | None -> return Error DidNotAnswer
        }

    /// advisor-[030]: settle only the fenced terminal. A Completed from an
    /// older provider run (a sticky terminal or the view prompt finishing
    /// first) is observation only; the first Completed after the fence is
    /// this consult's terminal, older or repeated ones never answer.
    let private settleTerminal (fence: ConsultFence) (outcome: TerminalOutcome) : Result<unit, string> option =
        match outcome with
        | Completed result when result.ProviderRun = fence.ProviderRun -> Some(Ok())
        | Completed _ -> None
        | Aborted _
        | Failed _ -> Some(Error DidNotAnswer)

    let private settledAdvice
        (deps: AdvisorSeatDeps)
        (childId: SessionId)
        (completion: TaskCompletionSource<Result<unit, string>>)
        : Task<Result<string, string>> =
        task {
            let! settled = completion.Task

            match settled with
            | Error reason -> return Error reason
            | Ok() -> return! readAdvice deps childId
        }

    let private awaitAdvisorProse
        (deps: AdvisorSeatDeps)
        (childId: SessionId)
        (fence: ConsultFence)
        (completion: TaskCompletionSource<Result<unit, string>>)
        : Task<Result<string, string>> =
        task {
            let! terminalFirst = NodeTiming.raceExit (completion.Task :> Task) ConsultTimeoutMs

            if terminalFirst then
                return! settledAdvice deps childId completion
            else
                return Error DidNotAnswer
        }

    /// advisor-[002]: two physical prompts — the caller's view, then this consult's intent.
    let private sendThenAwait
        (deps: AdvisorSeatDeps)
        (childId: SessionId)
        (fence: ConsultFence)
        (completion: TaskCompletionSource<Result<unit, string>>)
        (send: string -> Task<Result<PromptKey, string>>)
        (viewPrompt: string)
        (intent: string)
        : Task<Result<string, string>> =
        taskResult {
            let! _ = send viewPrompt
            let! _ = send intent
            return! awaitAdvisorProse deps childId fence completion
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
            let dispatcher = PromptDispatcher.forPrompts (PromptJournalAdapter.create journal)
            let port = DispatchSessionPort.ofSessionPort deps.Sessions

            let issueIdentitySeed () =
                PromptAuthorityProjectionQueries.issueCurrentOwnerIdentitySeed
                    (AgentJournal.snapshot journal).AgentProjections
                    (SessionId.create callerSessionId)
                    deps.Participant

            let completion =
                TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            // advisor-[030]: consult fence. The fence starts at the prior
            // consult's recorded provider run (empty for the first consult)
            // and advances to the intent prompt's own run once its send is
            // accepted, so a consult only ever accepts the terminal of the
            // intent it sent — never a sticky terminal or an older consult's.
            let fence =
                { ProviderRun = priorRun childId |> Option.defaultValue (ProviderRunIdentity.create "")
                  ConsultId = Guid.NewGuid().ToString("N") }

            let mutable intentRun = None

            // AG-LISTENER-BEFORE-SEND: the Host port refuses a prompt for a session with no
            // live terminal listener, and the listener must outlive both sends.
            let onTerminal _ outcome =
                match intentRun with
                | Some providerRun -> recordFence childId providerRun fence.ConsultId
                | None -> ()

                match settleTerminal fence outcome with
                | Some verdict -> AsyncSupport.trySetResult completion verdict |> ignore
                | None -> ()

            use _subscription = deps.Sessions.SubscribeTerminal(childId, onTerminal)

            let send text =
                dispatcher.SendManagedAssignment port childId text issueIdentitySeed deps.Directory None

            let! sent =
                taskResult {
                    let! _ = send viewPrompt
                    let! key = send intent

                    // The intent receipt is the fence's run identity: the
                    // terminal answering this prompt carries it.
                    intentRun <- Some(ProviderRunIdentity.create (PromptKey.value key))

                    return! awaitAdvisorProse deps childId fence completion
                }

            // advisor-[030]: the recorded run advances only after the intent
            // send was accepted, so a later consult beats exactly the terminal
            // this consult awaited.
            match intentRun with
            | Some providerRun -> recordFence childId providerRun fence.ConsultId
            | None -> ()

            return sent
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

    /// advisor-[030] test/dev hook: drop every recorded consult fence.
    let clearConsultFences () : unit = consultFences.Clear()
