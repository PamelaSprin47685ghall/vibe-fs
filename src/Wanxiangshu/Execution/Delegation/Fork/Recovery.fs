namespace Wanxiangshu.Execution.Delegation.Fork

open System
open Wanxiangshu.Foundation.Identity

/// Rebuilds physical ChildRun aggregates from durable linkage after restart.
/// No runtime id or synthetic running state participates in ownership.
/// P0-RECOVERY-JOIN-001: restore/interrupt never mint durable or cell finality.
module ForkRecovery =

    /// Re-register Active run after restart. Completion cell stays open.
    /// `createdAt` is caller-minted (IClockPort at composition).
    let restore agentId agentName role (createdAt: DateTimeOffset) agents =
        let runId = "restored-" + agentId

        let run =
            ChildRun.create agentId runId agentName role "(restored from journal)" createdAt

        Map.add agentId run agents

    /// Cancel in-flight busy work only. Keep handle Active; do not fill completion.
    let markInterrupted (agentId: string) (_reason: string) (agents: Map<string, ChildRun>) =
        match Map.tryFind agentId agents with
        | None -> agents
        | Some run ->
            ChildRun.cancel run
            agents

    let bindChildSession (agentId: string) (childSessionId: SessionId) (agents: Map<string, ChildRun>) =
        match Map.tryFind agentId agents with
        | Some run -> ChildRun.bindSession run childSessionId
        | None -> ()

        agents
