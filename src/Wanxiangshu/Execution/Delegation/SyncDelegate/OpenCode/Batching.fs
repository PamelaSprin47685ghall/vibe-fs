namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    [<Literal>]
    let MergedReference = "tool/sync-delegate/merged-reference"

    let private batchOfMessage providerRun role currentCall (message: SessionMessage) =
        let callOrder =
            message.ToolParts
            |> Array.choose (fun part ->
                part.ToolName
                |> SyncDelegate.tryRoleOfToolName
                |> Option.filter (fun partRole -> partRole = role)
                |> Option.map (fun _ -> part.ToolCallId))
            |> Array.toList

        if callOrder |> List.exists (fun callId -> callId = currentCall) then
            Some
                { ProviderRun = providerRun
                  CallOrder = callOrder
                  CurrentCall = currentCall }
        else
            None

    let private batchOfSnapshotMessages providerRun role currentCall messages =
        let providerRunKey = ProviderRunIdentity.value providerRun

        messages
        |> List.tryFind (fun message -> message.Id = providerRunKey)
        |> Option.bind (batchOfMessage providerRun role currentCall)

    let private tryReadMessages (snapshot: ISessionSnapshotPort) (owner: SessionId) : Task<SessionMessage list option> =
        task {
            match! snapshot.GetMessages owner with
            | Error _ -> return None
            | Ok messages -> return Some messages
        }

    let private resolveFromSnapshot
        (scope: ToolRuntimeScope)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity)
        (role: SyncDelegateRole)
        (currentCall: ToolCallId)
        : Task<SyncDelegateBatch option> =
        task {
            match scope.Snapshot with
            | None -> return None
            | Some snapshot ->
                let! messages = tryReadMessages snapshot owner
                return messages |> Option.bind (batchOfSnapshotMessages providerRun role currentCall)
        }

    let private callKey (callId: ToolCallId) = ToolCallId.value callId

    let private isPrefix (left: ToolCallId list) (right: ToolCallId list) =
        let leftKeys = left |> List.map callKey
        let rightKeys = right |> List.map callKey

        leftKeys.Length <= rightKeys.Length
        && leftKeys = (rightKeys |> List.take leftKeys.Length)

    let private longerBatch (observedBatch: SyncDelegateBatch) (snapshotBatch: SyncDelegateBatch) : SyncDelegateBatch =
        if observedBatch.CallOrder.Length >= snapshotBatch.CallOrder.Length then
            observedBatch
        else
            snapshotBatch

    let private moreCompleteBatch
        (observed: SyncDelegateBatch option)
        (snapshot: SyncDelegateBatch option)
        : SyncDelegateBatch option =
        match observed, snapshot with
        | None, None -> None
        | Some batch, None
        | None, Some batch -> Some batch
        | Some observedBatch, Some snapshotBatch when isPrefix observedBatch.CallOrder snapshotBatch.CallOrder ->
            Some snapshotBatch
        | Some observedBatch, Some snapshotBatch when isPrefix snapshotBatch.CallOrder observedBatch.CallOrder ->
            Some observedBatch
        | Some observedBatch, Some snapshotBatch -> Some(longerBatch observedBatch snapshotBatch)

    let private resolveBatch
        (runtime: SyncDelegateRuntime)
        (scope: ToolRuntimeScope)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity)
        (role: SyncDelegateRole)
        (currentCall: ToolCallId)
        : Task<SyncDelegateBatch option> =
        task {
            let observed = runtime.TryObservedBatch(owner, providerRun, role, currentCall)
            let! snapshot = resolveFromSnapshot scope owner providerRun role currentCall
            return moreCompleteBatch observed snapshot
        }

    let resolve
        (runtime: SyncDelegateRuntime)
        (scope: ToolRuntimeScope)
        (role: SyncDelegateRole)
        (context: HostToolContext)
        =
        task {
            match context.ProviderRunId, context.ToolCallId with
            | Some providerRun, Some currentCall when not (String.IsNullOrWhiteSpace context.SessionId) ->
                let owner = SessionId.create context.SessionId
                return! resolveBatch runtime scope owner providerRun role currentCall
            | _ -> return None
        }

    let mergedInstruction language canonicalCall =
        ProviderProse.render language MergedReference (Map [ "call", ToolCallId.value canonicalCall ])
