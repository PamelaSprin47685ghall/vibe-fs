namespace Wanxiangshu.Next.Session

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Agent

/// Single-assignment completion cell — the one place a child run's final
/// result is written.  The first successful TrySet wins; subsequent calls are
/// idempotent no-ops.
type CompletionCell<'a>() =
    let tcs = TaskCompletionSource<'a>(TaskCreationOptions.RunContinuationsAsynchronously)
    let mutable completed = false
    let mutable stored: 'a option = None
    let gate = obj ()

    member _.TrySet(value: 'a) : bool =
        lock gate (fun () ->
            if completed then
                false
            else
                // Fable's Task.Result is not a reliable completed-value API.
                // Keep the single physical completion value beside its signal.
                stored <- Some value
                completed <- true
                tcs.SetResult(value)
                true)

    member _.Await: Task<'a> = tcs.Task

    member _.IsCompleted: bool =
        lock gate (fun () -> completed)

    member _.StoredValue: 'a option = lock gate (fun () -> stored)

module CompletionCell =
    let create<'a> () : CompletionCell<'a> = CompletionCell<'a>()

/// One physical child run — single owner of the completion cell and
/// cancellation lifetime.  The run is created for each fork/new-owner attempt;
/// a busy nudge does not create a new run.
type ChildRun =
    { /// Runtime identity used to key the agent in the ForkRuntime map.
      AgentId: string

      /// Unique identity for this run attempt (e.g. "run-a1b2c3d4").
      RunId: string

      /// The managed agent name (e.g. "fast-coder", "deep-reviewer").
      AgentName: string

      /// Canonical role of the agent.
      Role: AgentRole

      /// The prompt text sent to the agent.
      Prompt: string

      /// The Host SessionId of the child session, once created or recovered.
      mutable ChildSessionId: string option

      /// Single-assignment completion: set exactly once when the run finishes.
      Completion: CompletionCell<RunCompletion>

      /// Cancellation handle for the run. Cancelled on parent Cancel or abort.
      Cancellation: CancellationTokenSource

      /// When this run was created.
      CreatedAt: DateTimeOffset }

module ChildRun =

    /// Create a new ChildRun with the given identity and agent metadata.
    let create
        (agentId: string)
        (runId: string)
        (agentName: string)
        (role: AgentRole)
        (prompt: string)
        : ChildRun =
        { AgentId = agentId
          RunId = runId
          AgentName = agentName
          Role = role
          Prompt = prompt
          ChildSessionId = None
          Completion = CompletionCell.create ()
          Cancellation = new CancellationTokenSource()
          CreatedAt = DateTimeOffset.UtcNow }

    let isActive (run: ChildRun) : bool =
        not run.Cancellation.IsCancellationRequested
        && not (run.Completion.IsCompleted)

    let isCompleted (run: ChildRun) : bool =
        run.Completion.IsCompleted

    let isCancelled (run: ChildRun) : bool =
        run.Cancellation.IsCancellationRequested

    let cancel (run: ChildRun) : unit =
        if not run.Cancellation.IsCancellationRequested then
            run.Cancellation.Cancel()

    let bindSession (run: ChildRun) (sessionId: string) : unit =
        run.ChildSessionId <- Some sessionId

    let makeCompleted (run: ChildRun) (outcome: AgentCompletionOutcome) : RunCompletion =
        { RunId = run.RunId
          AgentId = run.AgentId
          AgentName = run.AgentName
          Role = run.Role
          Outcome = AgentCompletion.withRunIdentity run.AgentId run.RunId run.Role outcome
          CompletedAt = DateTimeOffset.UtcNow }

    let makeFailed (run: ChildRun) (message: string) : RunCompletion =
        { RunId = run.RunId
          AgentId = run.AgentId
          AgentName = run.AgentName
          Role = run.Role
          Outcome =
            AgentCompletion.failed
                run.AgentId
                run.RunId
                (Some run.Role)
                run.ChildSessionId
                "ERROR"
                message
          CompletedAt = DateTimeOffset.UtcNow }

    let makeAborted (run: ChildRun) (reason: string) : RunCompletion =
        { RunId = run.RunId
          AgentId = run.AgentId
          AgentName = run.AgentName
          Role = run.Role
          Outcome =
            AgentCompletion.aborted
                run.AgentId
                run.RunId
                (Some run.Role)
                run.ChildSessionId
                "CANCELLED"
                reason
          CompletedAt = DateTimeOffset.UtcNow }

    /// Try to complete this run with the given RunCompletion.  Returns true
    /// if this is the first (and only) write.  Idempotent no-op after first set.
    let tryComplete (run: ChildRun) (completion: RunCompletion) : bool =
        run.Completion.TrySet(completion)

/// The canonical agent program for running a child to its single completion.
/// This is where `agent {}` is actually invoked by the child/fork production
/// path; the orphan showcase `AgentProgram` module has been removed.
module ChildRunProgram =

    let private fromTask (f: CancellationToken -> Task<AgentCompletionOutcome>) : AgentFlow<AgentCompletionOutcome> =
        Flow.create (fun _ ct ->
            task {
                if ct.IsCancellationRequested then
                    return Error AgentError.ParentCancelled
                else
                    try
                        let! outcome = f ct
                        return Ok outcome
                    with
                    | :? OperationCanceledException ->
                        return Error AgentError.ParentCancelled
                    | ex ->
                        return Error(AgentError.HostFailure ex.Message)
            })

    /// Build the AgentFlow that runs `work` and returns the resulting
    /// RunCompletion.  Cancellation and exceptions are mapped into a
    /// single-assignment RunCompletion value by the caller (ForkRuntime).
    let run
        (run: ChildRun)
        (work: CancellationToken -> Task<AgentCompletionOutcome>)
        : AgentFlow<RunCompletion> =
        agent {
            // Validate the managed identity through the canonical AgentProgram
            // before executing the physical child work.
            let! identityOk =
                Flow.lift (fun _ _ ->
                    task {
                        let! result =
                            AgentProgram.runAgentFlow
                                { SessionId = run.AgentId
                                  AgentName = run.AgentName }
                                CancellationToken.None
                                (AgentProgram.validateSession run.AgentName)

                        return result |> Result.defaultValue false
                    })

            if not identityOk then
                return! Flow.fail (AgentError.InvalidFork "Child run identity does not match managed agent")
            else
                let! outcome = fromTask work
                return ChildRun.makeCompleted run outcome
        }
