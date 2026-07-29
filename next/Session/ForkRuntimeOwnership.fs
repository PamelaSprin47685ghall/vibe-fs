namespace Wanxiangshu.Next.Session

/// Ownership checks for completions delivered from external resources. Normal
/// agent runs enqueue their own completion directly inside ForkRuntime.
module ForkRuntimeOwnership =

    let private childSessionId outcome =
        match outcome with
        | AgentCompleted payload -> Some payload.ChildSessionId
        | AgentFailed payload
        | AgentAborted payload -> payload.ChildSessionId

    let ownsPublishedCompletion
        (agents: System.Collections.Generic.Dictionary<string, AgentRecord>)
        (ptys: System.Collections.Generic.Dictionary<string, PtyRecord>)
        (completion: RunCompletion)
        =
        match ptys.TryGetValue completion.RunId with
        | true, _ -> true
        | false, _ ->
            match agents.TryGetValue completion.AgentId with
            | true, record ->
                match record.CurrentRunId with
                | Some runId -> runId = completion.RunId
                | None ->
                    match record.ChildSessionId, childSessionId completion.Outcome with
                    | Some ownedChild, Some completedChild
                        when
                            record.Status = AgentStatus.Idle
                            && Option.isNone record.LastCompletionStatus
                            && ownedChild = completedChild ->
                        true
                    | _ -> false
            | false, _ -> false
