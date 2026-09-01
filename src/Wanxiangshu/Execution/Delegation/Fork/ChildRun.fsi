namespace Wanxiangshu.Execution.Delegation.Fork

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Execution.Agent
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type CompletionCell<'a> =
    new: unit -> CompletionCell<'a>
    member TrySet: value: 'a -> bool
    member Await: Task<'a>
    member IsCompleted: bool
    member StoredValue: 'a option

module CompletionCell =
    val create<'a> : unit -> CompletionCell<'a>

type ChildRun =
    { AgentId: string
      RunId: string
      AgentName: string
      Role: Role
      Prompt: string
      mutable ChildSessionId: SessionId option
      Completion: CompletionCell<RunCompletion>
      Cancellation: CancellationTokenSource
      CreatedAt: DateTimeOffset }

module ChildRun =
    val create:
        agentId: string ->
        runId: string ->
        agentName: string ->
        role: Role ->
        prompt: string ->
        createdAt: DateTimeOffset ->
            ChildRun

    val isActive: run: ChildRun -> bool
    val isCompleted: run: ChildRun -> bool
    val isCancelled: run: ChildRun -> bool
    val cancel: run: ChildRun -> unit
    val bindSession: run: ChildRun -> sessionId: SessionId -> unit

    val makeCompleted: run: ChildRun -> outcome: AgentCompletionOutcome -> completedAt: DateTimeOffset -> RunCompletion

    val makeFailed: run: ChildRun -> message: string -> completedAt: DateTimeOffset -> RunCompletion
    val tryComplete: run: ChildRun -> completion: RunCompletion -> bool

module ChildRunProgram =
    val run:
        run: ChildRun ->
        work: (CancellationToken -> Task<AgentCompletionOutcome>) ->
        ct: CancellationToken ->
        now: (unit -> DateTimeOffset) ->
            Task<Result<RunCompletion, AgentError>>
