namespace Wanxiangshu.Session

/// Pure view of one ChildRun's physical completion/cancellation state.
module ChildRunProjection =

    let status runtimeCancelled (run: ChildRun) =
        if run.Completion.IsCompleted then
            match run.Completion.StoredValue with
            | Some completion ->
                match completion.Outcome with
                | AgentFailed payload when payload.Code = "INTERRUPTED" -> AgentStatus.Interrupted
                | AgentAbandoned _ -> AgentStatus.Closed
                | _ -> AgentStatus.Idle
            | None -> AgentStatus.Idle
        elif runtimeCancelled || run.Cancellation.IsCancellationRequested then
            AgentStatus.Closed
        else
            AgentStatus.Busy

    let toRecord runtimeCancelled (agentId: string) (run: ChildRun) =
        let lastStatus =
            if run.Completion.IsCompleted then
                match run.Completion.StoredValue with
                | Some completion ->
                    match completion.Outcome with
                    | AgentFailed payload when payload.Code = "INTERRUPTED" -> Some payload.Message
                    | AgentAbandoned(_, reason) -> Some reason
                    | _ -> Some(AgentCompletion.status completion.Outcome)
                | None -> None
            else
                None

        { AgentId = agentId
          Agent = run.AgentName
          Role = run.Role
          Status = status runtimeCancelled run
          CurrentRunId = if run.Completion.IsCompleted then None else Some run.RunId
          LastCompletionStatus = lastStatus
          HasPendingCompletion = run.Completion.IsCompleted
          ChildSessionId = run.ChildSessionId }
