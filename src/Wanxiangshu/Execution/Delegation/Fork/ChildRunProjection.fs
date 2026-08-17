namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Pure view of one ChildRun's physical completion/cancellation state.
module ChildRunProjection =

    let private statusOfCompletion completion =
        match completion.Outcome with
        | AgentFailed payload when payload.Code = "INTERRUPTED" -> AgentStatus.Interrupted
        | AgentAbandoned _ -> AgentStatus.Closed
        | _ -> AgentStatus.Idle

    let private statusOfStoredValue storedValue =
        storedValue
        |> Option.map statusOfCompletion
        |> Option.defaultValue AgentStatus.Idle

    let private lastStatusOfCompletion completion =
        match completion.Outcome with
        | AgentFailed payload when payload.Code = "INTERRUPTED" -> Some payload.Message
        | AgentAbandoned(_, reason) -> Some reason
        | _ -> Some(AgentCompletion.status completion.Outcome)

    let private lastStatusOfStoredValue storedValue =
        storedValue |> Option.bind lastStatusOfCompletion

    let status runtimeCancelled (run: ChildRun) =
        if run.Completion.IsCompleted then
            statusOfStoredValue run.Completion.StoredValue
        elif runtimeCancelled || run.Cancellation.IsCancellationRequested then
            AgentStatus.Closed
        else
            AgentStatus.Busy

    let toRecord runtimeCancelled (agentId: string) (run: ChildRun) =
        let lastStatus =
            if run.Completion.IsCompleted then
                lastStatusOfStoredValue run.Completion.StoredValue
            else
                None

        { AgentId = agentId
          Agent = run.AgentName
          Role = run.Role
          Status = status runtimeCancelled run
          CurrentRunId = if run.Completion.IsCompleted then None else Some run.RunId
          TerminalStatusLabel = lastStatus
          CompletionCellSettled = run.Completion.IsCompleted
          ChildSessionId = run.ChildSessionId }
