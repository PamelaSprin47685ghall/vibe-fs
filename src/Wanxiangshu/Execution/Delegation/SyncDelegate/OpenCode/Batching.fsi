namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    [<Literal>]
    val MergedReference: string = "tool/sync-delegate/merged-reference"

    val resolve:
        runtime: SyncDelegateRuntime ->
        scope: ToolRuntimeScope ->
        role: SyncDelegateRole ->
        context: HostToolContext ->
            Task<SyncDelegateBatch option>

    val mergedInstruction: language: ProviderLanguage -> canonicalCall: ToolCallId -> string
