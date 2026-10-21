namespace Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Host

/// OpenCode binding of the host-neutral `SyncDelegateBatchRules`: the tool
/// context becomes a `DelegationCaller`, result instructions become the TOML
/// tool reply, and settled results are written into OpenCode message parts.
[<RequireQualifiedAccess>]
module SyncDelegateBatching =

    let private wire (instruction: string) =
        ToolResultText.tomlObjectWithInstructions [ instruction ] []

    let resolve
        (runtime: SyncDelegateRuntime)
        (snapshot: ISessionSnapshotPort option)
        (role: SyncDelegateRole)
        (context: HostToolContext)
        : Task<SyncDelegateBatch option> =
        SyncDelegateBatchRules.resolve
            runtime
            snapshot
            role
            { Session =
                if String.IsNullOrWhiteSpace context.SessionId then
                    None
                else
                    Some(SessionId.create context.SessionId)
              ProviderRunId = context.ProviderRunId
              ToolCallId = context.ToolCallId }

    let stageDeferredInspection
        (sessionId: string)
        (callId: ToolCallId)
        (charge: string)
        (keywords: string)
        (estimate: int option)
        : string =
        wire (SyncDelegateBatchRules.stageDeferredInspection sessionId callId charge keywords estimate)

    let settleDeferredInspections
        (runtime: SyncDelegateRuntime)
        (workspaceDirectory: string option)
        (sessionId: string)
        : Task<unit> =
        ignore workspaceDirectory
        SyncDelegateBatchRules.settleDeferredInspections runtime sessionId

    let private isToolPart (part: obj) =
        not (isNull part) && string (part?``type``) = "tool"

    let private replacePartStateOutput (part: obj) (replacement: string) =
        if not (isNull part?state) then
            part?state?output <- replacement

    let private replaceToolPart (part: obj) =
        match SyncDelegateBatchRules.tryReplacedResult (string (part?callID)) with
        | Some replacement -> replacePartStateOutput part (wire replacement)
        | None -> ()

    let private applyToolPart (part: obj) =
        if isToolPart part then
            replaceToolPart part

    let private applyPartReplacements (msg: obj) =
        unbox<obj array> msg?parts |> Array.iter applyToolPart

    let private toolMessageCallId (msg: obj) =
        if not (isNull msg?tool_call_id) then
            string msg?tool_call_id
        elif not (isNull msg?toolCallId) then
            string msg?toolCallId
        else
            ""

    let private applyToolMessageContent (msg: obj) =
        match SyncDelegateBatchRules.tryReplacedResult (toolMessageCallId msg) with
        | Some replacement -> msg?content <- wire replacement
        | None -> ()

    let private applyMessageReplacements (msg: obj) =
        if not (isNull msg) && not (isNull msg?parts) then
            applyPartReplacements msg
        elif not (isNull msg) && string (msg?role) = "tool" then
            applyToolMessageContent msg

    let applyReplacedResults (messages: obj list) : obj list =
        let replaceAll () =
            messages |> List.iter applyMessageReplacements
            messages

        if SyncDelegateBatchRules.hasReplacedResults () then
            replaceAll ()
        else
            messages
