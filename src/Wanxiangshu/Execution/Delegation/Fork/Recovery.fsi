namespace Wanxiangshu.Execution.Delegation.Fork

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module ForkRecovery =
    val restore:
        agentId: string ->
        agentName: string ->
        role: Role ->
        createdAt: DateTimeOffset ->
        agents: Map<string, ChildRun> ->
            Map<string, ChildRun>

    val markInterrupted: agentId: string -> _reason: string -> agents: Map<string, ChildRun> -> Map<string, ChildRun>

    val bindChildSession:
        agentId: string -> childSessionId: SessionId -> agents: Map<string, ChildRun> -> Map<string, ChildRun>
