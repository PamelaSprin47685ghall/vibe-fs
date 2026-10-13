namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Trace
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore

type DeliverResult =
    { Ok: bool
      Relayed: bool
      Delivered: bool
      RetiredIncumbencyId: string
      RetirementOutcome: string
      Digest: string
      Path: string
      ExecSessionId: string
      NextIncumbencyId: string
      Message: string }

/// planning-010: the outcome of one synchronous ask delegation.
type AskResult =
    { Kind: string
      WorkRecord: string
      Status: string }

module PlanTools =

    let private requireOpt (err: string) =
        function
        | Some v -> Ok v
        | None -> Error err

    let private resolvePathAndDigest
        (content: string)
        (root: string)
        (workId: string)
        : Result<string * string, string> =
        match PlanPath.resolvePlanPath root workId with
        | Error err -> Error(sprintf "Cannot resolve plan path: %s" err)
        | Ok planPath ->
            let digest = HostDigest.sha256Hex content
            Ok(digest, planPath)

    let private readAndValidatePlan (root: string) (workId: string) : Result<string * string * string, string> =
        match PlanPath.readPlan root workId with
        | Error err -> Error(sprintf "Cannot deliver: plan artifact is empty or missing (%s)" err)
        | Ok content when String.IsNullOrWhiteSpace content -> Error "Cannot deliver: plan artifact is empty"
        | Ok content ->
            resolvePathAndDigest content root workId
            |> Result.map (fun (digest, planPath) -> (content, digest, planPath))

    let private spawnExecutionSessionFor
        (spawn: string -> string -> Task<string>)
        (planContent: string)
        : Task<Result<string, string>> =
        task {
            try
                let! execSessionId = spawn "manager" planContent
                return Ok execSessionId
            with ex ->
                return Error(sprintf "Execution session spawn failed: %s" ex.Message)
        }

    /// The one lifecycle work this session owns, read from the same workspace
    /// EventStore the plan entry writes.
    let private lifecycleWorkOf
        (store: IEventStore)
        (workId: string)
        : Wanxiangshu.Mission.Lifecycle.LifecycleWork option =
        match store.TryCurrent "Lifecycle" with
        | None -> None
        | Some current ->
            let works = unbox<Map<string, Wanxiangshu.Mission.Lifecycle.LifecycleWork>> current
            Map.tryFind workId works

    let private commitLifecycleDeliver
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (fence: Wanxiangshu.Mission.Lifecycle.HostFence)
        (retirementCursor: int64)
        (blockerDigest: string option)
        (draft: Wanxiangshu.Mission.Lifecycle.DeliverDraft option)
        (startedSessionId: string option)
        : Task<Result<DeliverResult, string>> =
        task {
            match
                Wanxiangshu.Mission.Lifecycle.LifecycleKernel.deliverOneshot
                    HostDigest.sha256Hex
                    work
                    fence
                    (Some retirementCursor)
                    blockerDigest
                    Wanxiangshu.Mission.Lifecycle.SessionLookup.None
                    draft
                    startedSessionId
            with
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse reason -> return Error reason
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done label -> return Error label
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, label) ->
                match! Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit store workId events with
                | Ok Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendDurable ->
                    let nextIncumbencyId =
                        events
                        |> List.tryPick (function
                            | Wanxiangshu.Mission.Lifecycle.LifecycleEvent.IncumbencyOpened(_, id, _, _) -> Some id
                            | _ -> None)
                        |> Option.defaultValue ""

                    let digest = draft |> Option.map (fun value -> value.Digest) |> Option.defaultValue ""
                    let path = draft |> Option.map (fun value -> value.Path) |> Option.defaultValue ""
                    let execSessionId = startedSessionId |> Option.defaultValue ""

                    let message =
                        match label with
                        | "relayed" -> "Relayed to the next incumbency"
                        | "delivered" -> "Plan delivered"
                        | "blocked" -> "Retirement is blocked by live resources; nothing was retired"
                        | other -> other

                    return
                        Ok
                            { Ok = true
                              Relayed = label = "relayed"
                              Delivered = label = "delivered"
                              RetiredIncumbencyId = ""
                              RetirementOutcome =
                                if label = "relayed" then "Relayed"
                                elif label = "delivered" then "Delivered"
                                else ""
                              Digest = digest
                              Path = path
                              ExecSessionId = execSessionId
                              NextIncumbencyId = nextIncumbencyId
                              Message = message }
                | Ok _ -> return Error "deliver was not committed"
                | Error reason -> return Error reason
        }

    let executeDeliver
        (store: IEventStore)
        (root: string)
        (workId: string)
        (incumbencyId: string)
        (providerRunId: string)
        (toolCallId: string)
        (hasBlocker: bool)
        (spawn: string -> string -> Task<string>)
        (retirementCursor: int64)
        : Task<Result<DeliverResult, string>> =
        task {
            match lifecycleWorkOf store workId with
            | None -> return Error "lifecycle work is missing"
            | Some work ->
                match work.Active with
                | None -> return Error "No active incumbency found for this work"
                | Some active when active.Id <> incumbencyId ->
                    return
                        Error(
                            sprintf
                                "Active incumbency ID '%s' does not match invocation target '%s'"
                                active.Id
                                incumbencyId
                        )
                | Some _ ->
                    let fence =
                        { Wanxiangshu.Mission.Lifecycle.HostFence.ProviderRunId = Some providerRunId
                          Wanxiangshu.Mission.Lifecycle.HostFence.ToolCallId = Some toolCallId }

                    let blockerDigest = if hasBlocker then Some "retirement-blocked" else None

                    let authorized =
                        match Wanxiangshu.Mission.Lifecycle.LifecycleFold.folded work with
                        | Ok folded -> folded.DeliveryAuthorized
                        | Error _ -> false

                    if not authorized || hasBlocker then
                        return! commitLifecycleDeliver store workId work fence retirementCursor blockerDigest None None
                    else
                        match readAndValidatePlan root workId with
                        | Error err -> return Error err
                        | Ok(content, digest, planPath) ->
                            match! spawnExecutionSessionFor spawn content with
                            | Error err -> return Error err
                            | Ok execSessionId ->
                                let draft: Wanxiangshu.Mission.Lifecycle.DeliverDraft =
                                    { Missing = false
                                      Empty = false
                                      Digest = digest
                                      Path = planPath }

                                return!
                                    commitLifecycleDeliver
                                        store
                                        workId
                                        work
                                        fence
                                        retirementCursor
                                        blockerDigest
                                        (Some draft)
                                        (Some execSessionId)
        }

    // planning-010: at most one pending ask delegation per work. The tracker
    // is process-local memory — crash recovery has no hanging ask state
    // (planning-012), so this never becomes a durable state machine.
    // DSL-MUTABLE: resource — in-flight ask delegation gate
    let private pendingAskGate = obj ()
    // DSL-MUTABLE: resource — works with one pending ask delegation
    let private pendingAskWorks = System.Collections.Generic.HashSet<string>()

    let private tryBeginPendingAsk (workId: string) : bool =
        lock pendingAskGate (fun () -> pendingAskWorks.Add workId)

    let private clearPendingAsk (workId: string) : unit =
        lock pendingAskGate (fun () -> pendingAskWorks.Remove workId |> ignore)

    let private validateAskWork
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (intent: string)
        : Result<unit, string> =
        match work.CurrentDelivered, work.Active, String.IsNullOrWhiteSpace intent with
        | true, _, _ -> Error "The planning artifact has already been delivered. No further questions are permitted."
        | false, _, true -> Error "Intent cannot be empty"
        | false, None, _ -> Error "No active incumbency found for this work"
        | false, Some _, false -> Ok()

    let private captureDelegation (delegateTo: string -> Task<string>) (intent: string) : Task<Result<string, string>> =
        task {
            try
                let! record = delegateTo intent
                return Ok record
            with ex ->
                return Error(sprintf "DevOps delegation failed: %s" ex.Message)
        }

    // The binding is persisted single-flight after the delegation returns.
    // An already-bound work is Done and writes nothing (planning-010).
    let private ensureDevOpsBound
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        : Task<Result<unit, string>> =
        task {
            match
                Wanxiangshu.Mission.Lifecycle.LifecycleKernel.bindDevOps
                    work
                    Wanxiangshu.Mission.Lifecycle.BindTrigger.Ask
                    (PlanWorkOpen.deriveDevOpsId workId)
                    None
            with
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Done _ -> return Ok()
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Refuse reason -> return Error reason
            | Wanxiangshu.Mission.Lifecycle.KernelDecision.Write(events, _) ->
                match! Wanxiangshu.Mission.Lifecycle.LifecycleKernel.commit store workId events with
                | Ok Wanxiangshu.Mission.Lifecycle.CommitOutcome.AppendDurable -> return Ok()
                | Ok _ -> return Error "Failed to bind plan DevOps: deliver was not committed"
                | Error reason -> return Error(sprintf "Failed to bind plan DevOps: %s" reason)
        }

    let private bindDevOpsAndComplete
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (record: string)
        : Task<Result<AskResult, string>> =
        task {
            match! ensureDevOpsBound store workId work with
            | Error err -> return Error err
            | Ok() ->
                return
                    Ok
                        { Kind = "delegated"
                          WorkRecord = record
                          Status = "completed" }
        }

    let private finishPendingAsk
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (delegation: Result<string, string>)
        : Task<Result<AskResult, string>> =
        task {
            match delegation with
            | Error err -> return Error err
            | Ok record when String.IsNullOrWhiteSpace record ->
                return Error "The DevOps delegation returned an empty work record"
            | Ok record -> return! bindDevOpsAndComplete store workId work record
        }

    let private runPendingAsk
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        task {
            try
                let! delegation = captureDelegation delegateTo intent
                let! completed = finishPendingAsk store workId work delegation
                return completed
            finally
                clearPendingAsk workId
        }

    let private beginPendingAsk
        (store: IEventStore)
        (workId: string)
        (work: Wanxiangshu.Mission.Lifecycle.LifecycleWork)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        if not (tryBeginPendingAsk workId) then
            Task.FromResult(
                Error(sprintf "An ask delegation is already pending for this work (%s); wait for it to finish" workId)
            )
        else
            runPendingAsk store workId work intent delegateTo

    let executeAsk
        (store: IEventStore)
        (workId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        match lifecycleWorkOf store workId with
        | None -> Task.FromResult(Error "lifecycle work is missing")
        | Some work ->
            match validateAskWork work intent with
            | Error err -> Task.FromResult(Error err)
            | Ok() -> beginPendingAsk store workId work intent delegateTo

    let executeJsPlan
        (root: string)
        (workId: string)
        (action: string)
        (content: string option)
        (patches: (string * string) list option)
        : Result<string, string> =
        match action.ToLowerInvariant() with
        | "read"
        | "js-plan.read" -> PlanPath.readPlan root workId
        | "rewrite"
        | "js-plan.rewrite" ->
            content
            |> requireOpt "Missing content for rewrite"
            |> Result.bind (PlanPath.rewritePlanAtomic root workId)
            |> Result.map (fun (path, digest) -> sprintf "Plan rewritten at %s (digest: %s)" path digest)
        | "edit"
        | "js-plan.edit" ->
            patches
            |> requireOpt "Missing patches for edit"
            |> Result.bind (PlanPath.editPlanAtomic root workId)
            |> Result.map (fun (path, digest) -> sprintf "Plan edited at %s (digest: %s)" path digest)
        | _ -> Error(sprintf "Unknown js-plan action: %s" action)
