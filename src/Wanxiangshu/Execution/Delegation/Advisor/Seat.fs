namespace Wanxiangshu.Execution.Delegation.Advisor

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
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


    /// DSL-MUTABLE: resource — which prompt of the consult is in flight. The view prompt
    /// runs first and the intent prompt only after the view's own terminal closed its run.
    [<RequireQualifiedAccess>]
    type private ConsultPhase =
        | View
        | Intent

    /// AGENT-001: the managed agent this seat's child runs as.
    let private advisorAgent = ManagedAgent.nameOf Role.Advisor

    /// advisor-[030] consult fence. A consult answers exactly the intent prompt it sent, so
    /// the fence is bound to that prompt's identity (F39-01). A terminal names the provider
    /// run that reached it (HOST-010/011: the Host assistant message id); the run answers the
    /// physical user message that is its parent, and that message carries the `PromptKey`
    /// the dispatcher stamped on it (snapshot metadata, else the journal's physical landing).
    /// `PriorRun` is the run the previous consult on this child settled on, so a replayed or
    /// re-delivered Completed of an earlier consult is refused even before attribution.
    type private ConsultFence =
        { ChildId: SessionId
          PriorRun: ProviderRunIdentity option }

    /// DSL-MUTABLE: resource — child session id → provider run of the Completed the most
    /// recent consult on that child settled on. Process-live; the seat registry below has
    /// the same lifetime.
    let private settledRuns = Dictionary<string, ProviderRunIdentity>()

    let private recordSettled (childId: SessionId) (providerRun: ProviderRunIdentity) : unit =
        lock settledRuns (fun () -> settledRuns.[SessionId.value childId] <- providerRun)

    let private priorRun (childId: SessionId) : ProviderRunIdentity option =
        lock settledRuns (fun () ->
            match settledRuns.TryGetValue(SessionId.value childId) with
            | true, run -> Some run
            | false, _ -> None)

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

    [<AllowNullLiteral>]
    type private AdvisorAnswerLaw =
        abstract answer: messages: obj array -> string

    [<Import("AdvisorLaw", "wanxiangli/advisor")>]
    let private advisorLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : AdvisorAnswerLaw = jsNative

    /// The read-back law (last non-blank assistant prose, role case-insensitive) is owned by
    /// `wanxiangli/advisor` (CUT-39); this host only flattens Host message parts to text.
    let private answerLaw = construct advisorLawClass

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

    /// advisor-[030]: prose read-back after the fenced terminal. Only this
    /// consult's own terminal has been processed when this runs, so the last
    /// non-blank assistant message of the child is this consult's answer.
    let private latestAssistantProse (messages: SessionMessage list) : string option =
        messages
        |> List.map (fun message -> createObj [ "role" ==> message.Role; "text" ==> textOfParts message.Parts ])
        |> List.toArray
        |> answerLaw.answer
        |> Option.ofObj

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

    /// The prompt a physical user message carries: the dispatcher's key stamped in the Host
    /// snapshot first, the journal's accepted physical landing as its fallback.
    let private promptOfUserMessage
        (journal: AgentJournal)
        (childId: SessionId)
        (messages: SessionMessage list)
        (userMessageId: string)
        : string option =
        let stamped =
            messages
            |> List.tryFind (fun message -> message.Id = userMessageId)
            |> Option.bind (fun message -> message.PromptKey)

        let landed () =
            (AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind childId
            |> Option.bind (fun session -> session.PromptAuthority)
            |> Option.bind (fun authority ->
                Map.tryFind (PhysicalUserMessageId.create userMessageId) authority.PhysicalLandings)
            |> Option.map (fun landing -> PromptKey.value landing.PromptKey)

        stamped |> Option.orElseWith landed

    let private isAssistantRun (runId: string) (message: SessionMessage) : bool =
        message.Id = runId
        && String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)

    /// HOST-010/011 attribution: the prompt key of the user message the run's assistant
    /// message answers, or `None` when the Host gives no such evidence.
    let private promptAnsweredBy
        (journal: AgentJournal)
        (childId: SessionId)
        (messages: SessionMessage list)
        (run: ProviderRunIdentity)
        : string option =
        messages
        |> List.tryFind (isAssistantRun (ProviderRunIdentity.value run))
        |> Option.bind (fun message -> message.ParentId)
        |> Option.bind (promptOfUserMessage journal childId messages)

    let private okValue (result: Result<'value, 'error>) : 'value option =
        match result with
        | Ok value -> Some value
        | Error _ -> None

    let private attributeRun
        (deps: AdvisorSeatDeps)
        (journal: AgentJournal)
        (childId: SessionId)
        (run: ProviderRunIdentity)
        : Task<string option> =
        match deps.Snapshot with
        | None -> Task.FromResult None
        | Some snapshot ->
            task {
                let! messages = snapshot.GetMessages childId

                return
                    okValue messages
                    |> Option.bind (fun messages -> promptAnsweredBy journal childId messages run)
            }

    /// advisor-[030]: does an attributed Completed answer this consult's intent? A run that
    /// answers another prompt (this consult's view, an earlier consult's intent) never does.
    /// Without Host attribution the first fresh Completed after the intent was accepted is
    /// taken, which is the only order evidence left.
    let private answersIntent (intentKey: PromptKey) (attributed: string option) (afterIntent: bool) : bool =
        match attributed with
        | Some key -> key = PromptKey.value intentKey
        | None -> afterIntent

    /// advisor-[030]: a Completed of the run an earlier consult already settled on — a
    /// sticky replay or a duplicate delivery — is never this consult's terminal.
    let private isStaleRun (fence: ConsultFence) (result: AgentRunResult) : bool =
        fence.PriorRun |> Option.exists (fun prior -> prior = result.ProviderRun)

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

            // F39-02: the view prompt owns a run of its own. Its terminal ends the view
            // phase; only then is the child's run closed, so the intent prompt roots
            // freshly with a committed model-routing lease of its own.
            let viewCompletion =
                TaskCompletionSource<Result<unit, string>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let fence =
                { ChildId = childId
                  PriorRun = priorRun childId }

            // DSL-MUTABLE: resource — consult-local fence state. `armed` turns on just before
            // the first send, so the subscribe-time sticky replay (an earlier consult's
            // terminal) is observation only; `phase` says which prompt of the consult is
            // in flight; `intentKey` is the accepted intent prompt; `early` holds fresh
            // Completeds seen before the intent key was known.
            let armed = ref false
            let phase = ref ConsultPhase.View
            let intentKey: PromptKey option ref = ref None
            let early = ResizeArray<AgentRunResult>()

            let settle (result: AgentRunResult) (key: PromptKey) (afterIntent: bool) : Task<unit> =
                task {
                    let! attributed = attributeRun deps journal childId result.ProviderRun

                    if
                        answersIntent key attributed afterIntent
                        && AsyncSupport.trySetResult completion (Ok())
                    then
                        recordSettled childId result.ProviderRun
                }

            let admitCompleted (result: AgentRunResult) : unit =
                match intentKey.Value with
                | Some key -> settle result key true |> ignore
                | None -> lock early (fun () -> early.Add result)

            let observeCompleted (result: AgentRunResult) : unit =
                if armed.Value && not (isStaleRun fence result) then
                    admitCompleted result

            // F39-02: a view-phase terminal ends the view phase; an intent-phase terminal
            // is the advice candidate. The fence rejects a sticky replay (an earlier
            // consult's terminal) and any run that did not answer this consult, so it
            // can neither end the view phase nor answer the intent.
            let completeAction (result: AgentRunResult) : unit =
                let acceptable = armed.Value && not (isStaleRun fence result)

                match phase.Value with
                | ConsultPhase.View when acceptable -> AsyncSupport.trySetResult viewCompletion (Ok()) |> ignore
                | ConsultPhase.Intent when acceptable -> observeCompleted result
                | _ -> ()

            // AG-LISTENER-BEFORE-SEND: the Host port refuses a prompt for a session with no
            // live terminal listener, and the listener must outlive both sends.
            let onTerminal _ outcome =
                match outcome with
                | Completed result -> completeAction result
                | Aborted _
                | Failed _ when armed.Value ->
                    AsyncSupport.trySetResult completion (Error DidNotAnswer) |> ignore
                    AsyncSupport.trySetResult viewCompletion (Error DidNotAnswer) |> ignore
                | Aborted _
                | Failed _ -> ()

            use _subscription = deps.Sessions.SubscribeTerminal(childId, onTerminal)

            let sendManaged text =
                dispatcher.SendManagedAssignment port childId text issueIdentitySeed deps.Directory None

            // DSL-MUTABLE: resource — acceptance subscriptions the root send attaches; disposed
            // with the consult.
            let acceptance = ResizeArray<IDisposable>()

            let acceptanceObserver: ContinuationAcceptanceObserver =
                { Notify = ignore
                  AttachDisposable = fun subscription -> lock acceptance (fun () -> acceptance.Add subscription) }

            // advisor-[002] on a fresh seat (F39-01): the view opens the child's Authority Root,
            // so the dispatcher must see that root accepted before the intent is sent. A
            // detached root send returns before the Host admits it, and the intent would then be
            // claimed as a second AgentOwnerRoot, which chat admission rejects
            // (ActiveRunIdentityConflict). The root send therefore awaits physical acceptance,
            // as the fork's first prompt does.
            //
            // F39-02: the same rule governs the intent prompt. The view's run is closed before
            // the intent is sent, so the intent finds no live logical run and opens a fresh
            // root of its own — with a committed model-routing lease — instead of continuing a
            // finished run (EMR-010).
            let sendRoot (text: string) : Task<Result<PromptKey, string>> =
                match issueIdentitySeed () with
                | Error reason -> Task.FromResult(Error reason)
                | Ok identitySeed ->
                    dispatcher.SendAgentOwnerRoot
                        port
                        childId
                        text
                        identitySeed
                        deps.Directory
                        PromptDispatcher.AwaitMode.Await
                        (Some acceptanceObserver)

            /// A prompt joins the child's live logical run when it has one, and opens a fresh
            /// root when it has none.
            let sendPrompt (text: string) : Task<Result<PromptKey, string>> =
                match (dispatcher.ProjectionFor childId).ActiveLogicalRun with
                | Some _ -> sendManaged text
                | None -> sendRoot text

            /// A Host that never admits the view must not hang the caller's turn either.
            let boundedView () : Task<Result<PromptKey, string>> =
                task {
                    let viewSend = sendPrompt viewPrompt
                    let! admitted = NodeTiming.raceExit (viewSend :> Task) ConsultTimeoutMs

                    if admitted then
                        return! viewSend
                    else
                        return Error DidNotAnswer
                }

            /// F39-02: the view's own terminal must arrive before the intent is sent, and
            /// the view's run is closed as soon as it does. A terminal that never arrives
            /// refuses the consult, exactly like an intent that is never answered.
            let voidOnViewOutcome (outcome: Result<unit, string>) : Task<Result<unit, string>> =
                match outcome with
                | Error reason -> Task.FromResult(Error reason)
                | Ok() ->
                    // The close is durable: the dispatcher's projection must not see an
                    // ActiveLogicalRun, or the intent prompt would append to a finished
                    // run (EMR-010).
                    voidChildRun journal callerSessionId childId

            let closeAfterView () : Task<Result<unit, string>> =
                task {
                    let! arrived = NodeTiming.raceExit (viewCompletion.Task :> Task) ConsultTimeoutMs

                    // A terminal that never arrives refuses the consult, exactly like an
                    // intent that is never answered.
                    let! outcome =
                        if arrived then
                            viewCompletion.Task
                        else
                            Task.FromResult<Result<unit, string>>(Error DidNotAnswer)

                    return! voidOnViewOutcome outcome
                }

            let disposeAcceptance () =
                let subscriptions = lock acceptance (fun () -> Seq.toList acceptance)

                for subscription in subscriptions do
                    subscription.Dispose()

            let acceptIntent (key: PromptKey) : unit =
                intentKey.Value <- Some key
                let seen = lock early (fun () -> Seq.toList early)
                // A Completed seen before the intent was accepted answers it only on Host
                // attribution (the view prompt's own run finishes first in that window).
                for result in seen do
                    settle result key false |> ignore

            armed.Value <- true

            try
                return!
                    taskResult {
                        let! _ = boundedView ()

                        // F39-02: the view's run is closed when its own terminal arrives, so
                        // the intent prompt roots freshly instead of appending to a finished
                        // run (which has no committed model-routing lease and would fail
                        // closed with EMR-010).
                        let! _ = closeAfterView ()

                        phase.Value <- ConsultPhase.Intent

                        let! key = sendPrompt intent
                        acceptIntent key
                        return! awaitAdvisorProse deps childId fence completion
                    }
            finally
                disposeAcceptance ()
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
            let! created = ensureSeatChild deps (seatFor callerSessionId)

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
