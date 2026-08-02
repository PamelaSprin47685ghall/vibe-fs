namespace Wanxiangshu.Next.Session

open System
open Wanxiangshu.Next.Kernel.Identity

/// Rebuilds physical ChildRun aggregates from durable linkage after restart.
/// No runtime id or synthetic running state participates in ownership.
module ForkRecovery =

    let restore agentId agentName role agents =
        let runId = "restored-" + agentId
        let run = ChildRun.create agentId runId agentName role "(restored from journal)"

        let completion =
            { RunId = runId
              AgentId = agentId
              AgentName = agentName
              Role = role
              Outcome = AgentCompletion.ofSimpleText agentId runId role "(restored)"
              CompletedAt = DateTimeOffset.UtcNow }

        run.Completion.TrySet(completion) |> ignore
        Map.add agentId run agents

    let markInterrupted agentId reason agents =
        match Map.tryFind agentId agents with
        | None -> agents
        | Some run ->
            ChildRun.cancel run
            let runId = "interrupted-" + agentId
            let interrupted = ChildRun.create agentId runId run.AgentName run.Role run.Prompt
            run.ChildSessionId |> Option.iter (ChildRun.bindSession interrupted)

            let completion =
                { RunId = runId
                  AgentId = agentId
                  AgentName = run.AgentName
                  Role = run.Role
                  Outcome =
                    AgentCompletion.failed
                        agentId
                        runId
                        (Some run.Role)
                        run.ChildSessionId
                        "INTERRUPTED"
                        ("interrupted:" + reason)
                  CompletedAt = DateTimeOffset.UtcNow }

            interrupted.Completion.TrySet(completion) |> ignore
            Map.add agentId interrupted agents

    let bindChildSession agentId (childSessionId: SessionId) agents =
        match Map.tryFind agentId agents with
        | Some run -> ChildRun.bindSession run childSessionId
        | None -> ()

        agents
