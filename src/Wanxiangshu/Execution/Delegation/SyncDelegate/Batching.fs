namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Host.Domain
open Wanxiangshu.Participant.Provider

[<RequireQualifiedAccess>]
module SyncDelegateBatchRules =

    [<RequireQualifiedAccess>]
    module Path =
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
        (snapshot: ISessionSnapshotPort option)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity)
        (role: SyncDelegateRole)
        (currentCall: ToolCallId)
        : Task<SyncDelegateBatch option> =
        task {
            match snapshot with
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
        (snapshot: ISessionSnapshotPort option)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity)
        (role: SyncDelegateRole)
        (currentCall: ToolCallId)
        : Task<SyncDelegateBatch option> =
        task {
            let observed = runtime.TryObservedBatch(owner, providerRun, role, currentCall)
            let! snapshotBatch = resolveFromSnapshot snapshot owner providerRun role currentCall
            return moreCompleteBatch observed snapshotBatch
        }

    let resolve
        (runtime: SyncDelegateRuntime)
        (snapshot: ISessionSnapshotPort option)
        (role: SyncDelegateRole)
        (caller: DelegationCaller)
        =
        task {
            match caller.Session, caller.ProviderRunId, caller.ToolCallId with
            | Some owner, Some providerRun, Some currentCall ->
                return! resolveBatch runtime snapshot owner providerRun role currentCall
            | _ -> return None
        }

    let private mergedInstruction language canonicalCall =
        ProviderProse.render language Path.MergedReference (Map [ "call", ToolCallId.value canonicalCall ])

    type private DeferredCall =
        { CallId: ToolCallId
          Charge: string
          Keywords: string
          Estimate: int option }

    let private pendingInspections = Dictionary<string, ResizeArray<DeferredCall>>()
    let private durableReplacedResults = Dictionary<string, string>()

    [<Emit("Promise.all($0)")>]
    let private promiseAll (promises: Task<'T> array) : Task<'T array> = jsNative

    /// One charge's result instruction: the delivered view, the merged canonical
    /// call, or the failure prose. The three cases are the invocation result's
    /// own shape, not three separate decisions.
    let private renderInvocationOutput
        (language: ProviderLanguage)
        (result: Result<SyncDelegateInvocationResult, string>)
        : string =
        match result with
        | Ok(SyncDelegateInvocationResult.View view) -> ReturnedMaterial.render view
        | Ok(SyncDelegateInvocationResult.MergedInto canonicalCall) -> mergedInstruction language canonicalCall
        | Error err -> sprintf "Engineer charge failed: %s" err

    let private resolvePendingInspection (sessionId: string) : DeferredCall list option =
        lock pendingInspections (fun () ->
            match pendingInspections.TryGetValue sessionId with
            | true, list when list.Count > 0 ->
                let copy = list |> Seq.toList
                pendingInspections.Remove sessionId |> ignore
                Some copy
            | _ -> None)

    let stageDeferredInspection
        (sessionId: string)
        (callId: ToolCallId)
        (charge: string)
        (keywords: string)
        (estimate: int option)
        : string =
        lock pendingInspections (fun () ->
            let list =
                match pendingInspections.TryGetValue sessionId with
                | true, existing -> existing
                | false, _ ->
                    let created = ResizeArray<DeferredCall>()
                    pendingInspections.[sessionId] <- created
                    created

            list.Add
                { CallId = callId
                  Charge = charge
                  Keywords = keywords
                  Estimate = estimate }

            sprintf "Engineer charge accepted and deferred for batch execution: %s" charge)

    let settleDeferredInspections (runtime: SyncDelegateRuntime) (sessionId: string) : Task<unit> =
        task {
            match resolvePendingInspection sessionId with
            | None -> ()
            | Some calls ->
                let callOrder = calls |> List.map (fun c -> c.CallId)
                let combinedCharge = calls |> List.map (fun c -> c.Charge) |> String.concat "\n"

                let invoke (call: DeferredCall) =
                    let batch =
                        { ProviderRun = ProviderRunIdentity.create "deferred-batch"
                          CallOrder = callOrder
                          CurrentCall = call.CallId }

                    let preparePrompt () =
                        Task.FromResult(LlmFacing.instructions (calls |> List.map (fun c -> c.Charge)))

                    runtime.InvokeBatchPrepared(
                        sessionId,
                        SyncDelegateRole.Engineer,
                        combinedCharge,
                        batch,
                        preparePrompt,
                        ?expectedToolCalls = call.Estimate
                    )

                let tasks = calls |> List.map invoke
                let! results = promiseAll (List.toArray tasks)
                let lang = ProviderLanguageBinding.forSessionText sessionId

                lock durableReplacedResults (fun () ->
                    List.iteri
                        (fun i call ->
                            durableReplacedResults.[ToolCallId.value call.CallId] <-
                                renderInvocationOutput lang results.[i])
                        calls)
        }

    let hasReplacedResults () =
        lock durableReplacedResults (fun () -> durableReplacedResults.Count > 0)

    let tryReplacedResult (callId: string) =
        lock durableReplacedResults (fun () ->
            match durableReplacedResults.TryGetValue callId with
            | true, replacement -> Some replacement
            | false, _ -> None)
