namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    [<Literal>]
    val MergedReference: string = "tool/sync-delegate/merged-reference"

    val resolve:
        runtime: SyncDelegateRuntime ->
        snapshot: ISessionSnapshotPort option ->
        role: SyncDelegateRole ->
        context: HostToolContext ->
            Task<SyncDelegateBatch option>

    val mergedInstruction: language: ProviderLanguage -> canonicalCall: ToolCallId -> string

    val stageDeferredInspection:
        sessionId: string -> callId: ToolCallId -> charge: string -> keywords: string -> estimate: int option -> string

    val settleDeferredInspections:
        runtime: SyncDelegateRuntime -> workspaceDirectory: string option -> sessionId: string -> Task<unit>

    val applyReplacedResults: messages: obj list -> obj list
