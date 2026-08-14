namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
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
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    [<Literal>]
    let MergedReference = "tool/sync-delegate/merged-reference"

    let private roleOfToolName (name: string) =
        match name.Trim().ToLowerInvariant() with
        | "inspect" -> Some SyncDelegateRole.Inspector
        | "establish-behavior"
        | "repair-behavior" -> Some SyncDelegateRole.Coder
        | _ -> None

    let resolve (scope: ToolRuntimeScope) (role: SyncDelegateRole) (context: HostToolContext) =
        task {
            match scope.Snapshot, context.ProviderRunId, context.ToolCallId with
            | Some snapshot, Some providerRun, Some currentCall when not (String.IsNullOrWhiteSpace context.SessionId) ->
                match! snapshot.GetMessages(SessionId.create context.SessionId) with
                | Error _ -> return None
                | Ok messages ->
                    let providerRunKey = ProviderRunIdentity.value providerRun

                    match messages |> List.tryFind (fun message -> message.Id = providerRunKey) with
                    | None -> return None
                    | Some message ->
                        let callOrder =
                            message.ToolParts
                            |> Array.choose (fun part ->
                                match roleOfToolName part.ToolName with
                                | Some partRole when partRole = role -> Some part.ToolCallId
                                | _ -> None)
                            |> Array.toList

                        if callOrder |> List.exists (fun callId -> callId = currentCall) then
                            return
                                Some
                                    { ProviderRun = providerRun
                                      CallOrder = callOrder
                                      CurrentCall = currentCall }
                        else
                            return None
            | _ -> return None
        }

    let mergedInstruction language canonicalCall =
        ProviderProse.render language MergedReference (Map [ "call", ToolCallId.value canonicalCall ])
