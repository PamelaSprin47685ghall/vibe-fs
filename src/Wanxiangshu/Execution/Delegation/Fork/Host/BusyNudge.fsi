namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module HostForkBusyNudge =
    val send:
        sessions: ISessionHostPort ->
        _parentId: SessionId ->
        journal: AgentJournal option ->
        childId: SessionId ->
        _role: Role ->
        agent: string ->
        directory: string option ->
        prompt: string ->
            Task<Result<unit, string>>

    val sender:
        sessions: ISessionHostPort ->
        parentId: SessionId ->
        journal: AgentJournal option ->
        directoryOf: (string -> string option) ->
        agentId: string ->
        childId: SessionId ->
        role: Role ->
        agent: string ->
        prompt: string ->
            Task<Result<unit, string>>
