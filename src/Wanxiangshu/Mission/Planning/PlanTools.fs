namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
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

    let readAndValidatePlan (root: string) (workId: string) : Result<string * string * string, string> =
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
        (gitCommonDir: string)
        (workId: string)
        : Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option =
        if String.IsNullOrWhiteSpace gitCommonDir then
            None
        else
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId

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
        (gitCommonDir: string)
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
            if String.IsNullOrWhiteSpace gitCommonDir then
                return Error "git common directory is missing"
            else
                match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
                | None -> return Error "lifecycle work is missing"
                | Some work ->
                    match work.ActiveId with
                    | None -> return Error "No active incumbency found for this work"
                    | Some active when active <> incumbencyId ->
                        return
                            Error(
                                sprintf
                                    "Active incumbency ID '%s' does not match invocation target '%s'"
                                    active
                                    incumbencyId
                            )
                    | Some _ ->
                        let fence =
                            { Wanxiangshu.Mission.Lifecycle.HostFence.ProviderRunId = Some providerRunId
                              Wanxiangshu.Mission.Lifecycle.HostFence.ToolCallId = Some toolCallId }

                        let blockerDigest = if hasBlocker then Some "retirement-blocked" else None

                        // Stopped the old fold. Delivery authorization is already on the snapshot.
                        let authorized = work.DeliveryAuthorized

                        let requestId = providerRunId + ":" + toolCallId
                        let blockers = if hasBlocker then [| "retirement-blocked" |] else [| |]
                        let draftFields =
                            if hasBlocker || not authorized then
                                []
                            else
                                match readAndValidatePlan root workId with
                                | Error _ -> []
                                | Ok(_, digest, planPath) ->
                                    [ "draft", box {| digest = digest; path = planPath |} ]
                        let command =
                            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                                "Deliver"
                                ([ "incumbencyId", box incumbencyId
                                   "providerRunId", box providerRunId
                                   "toolCallId", box toolCallId
                                   "cursor", box (float retirementCursor)
                                   "blockers", box blockers ]
                                 @ draftFields)
                        let receipt =
                            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command
                        if receipt.Status = "Unknown" || receipt.Status = "NotWritten" || receipt.Status = "Conflict" then
                            return Error(if receipt.Reason = "" then receipt.Status else receipt.Reason)
                        elif receipt.Kind = "ReviewRequired" then
                            return Error "assessment required"
                        elif receipt.Status = "Committed" && receipt.Kind = "DeliveryRequested" then
                            let operationId = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "operationId"
                            let digest = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "digest"
                            let path = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "path"
                            match readAndValidatePlan root workId with
                            | Error err ->
                                let refused =
                                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                                        "ResolveDelivery"
                                        [ "operationId", box operationId
                                          "outcome", box "NotStarted"
                                          "reason", box err ]
                                let _ = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId operationId refused
                                return Error err
                            | Ok(content, _, _) ->
                                match! spawnExecutionSessionFor spawn content with
                                | Error err ->
                                    let refused =
                                        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                                            "ResolveDelivery"
                                            [ "operationId", box operationId
                                              "outcome", box "NotStarted"
                                              "reason", box err ]
                                    let _ = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId operationId refused
                                    return Error err
                                | Ok execSessionId ->
                                    let accepted =
                                        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                                            "ResolveDelivery"
                                            [ "operationId", box operationId
                                              "outcome", box "Accepted"
                                              "execSessionId", box execSessionId
                                              "digest", box digest
                                              "path", box path
                                              "blockers", box blockers ]
                                    let resolved =
                                        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId operationId accepted
                                    if resolved.Status <> "Committed" && resolved.Status <> "Replayed" then
                                        return Error(if resolved.Reason = "" then resolved.Status else resolved.Reason)
                                    else
                                        return
                                            Ok
                                                { Ok = true
                                                  Relayed = false
                                                  Delivered = resolved.Kind = "Delivered"
                                                  RetiredIncumbencyId = ""
                                                  RetirementOutcome = resolved.Kind
                                                  Digest = digest
                                                  Path = path
                                                  ExecSessionId = execSessionId
                                                  NextIncumbencyId = ""
                                                  Message = "Plan delivered" }
                        else
                            return
                                Ok
                                    { Ok = receipt.Status = "Committed" || receipt.Status = "Replayed"
                                      Relayed = receipt.Kind = "Relayed"
                                      Delivered = receipt.Kind = "Delivered"
                                      RetiredIncumbencyId = ""
                                      RetirementOutcome = receipt.Kind
                                      Digest = ""
                                      Path = ""
                                      ExecSessionId = ""
                                      NextIncumbencyId = ""
                                      Message = receipt.Kind }
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

    // Snapshot has no delivered mark. Do not treat delivery authorization as already delivered.
    let private validateAskWork
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (intent: string)
        : Result<unit, string> =
        if String.IsNullOrWhiteSpace intent then
            Error "Intent cannot be empty"
        elif work.ActiveId.IsNone then
            Error "No active incumbency found for this work"
        else
            Ok()

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
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (incumbencyId: string)
        : Task<Result<unit, string>> =
        task {
            let _ = store
            let requestId = workId + ":bind:" + incumbencyId
            let command =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                    "BindDevOps"
                    [ "incumbencyId", box incumbencyId
                      "trigger", box "Ask"
                      "devopsId", box (PlanWorkOpen.deriveDevOpsId workId)
                      "target", null ]
            let receipt =
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command
            if receipt.Status = "Committed" || receipt.Status = "Replayed" || receipt.Status = "Observed" then
                return Ok()
            else
                return Error(sprintf "Failed to bind plan DevOps: %s" (if receipt.Reason = "" then receipt.Status else receipt.Reason))
        }

    let private bindDevOpsAndComplete
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (activeId: string)
        (record: string)
        : Task<Result<AskResult, string>> =
        task {
            match! ensureDevOpsBound gitCommonDir store workId activeId with
            | Error err -> return Error err
            | Ok() ->
                return
                    Ok
                        { Kind = "delegated"
                          WorkRecord = record
                          Status = "completed" }
        }

    let private finishPendingAsk
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (activeId: string)
        (delegation: Result<string, string>)
        : Task<Result<AskResult, string>> =
        task {
            match delegation with
            | Error err -> return Error err
            | Ok record when String.IsNullOrWhiteSpace record ->
                return Error "The DevOps delegation returned an empty work record"
            | Ok record -> return! bindDevOpsAndComplete gitCommonDir store workId activeId record
        }

    let private runPendingAsk
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (activeId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        task {
            try
                let! delegation = captureDelegation delegateTo intent
                let! completed = finishPendingAsk gitCommonDir store workId activeId delegation
                return completed
            finally
                clearPendingAsk workId
        }

    let private beginPendingAsk
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (activeId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        if not (tryBeginPendingAsk workId) then
            Task.FromResult(
                Error(sprintf "An ask delegation is already pending for this work (%s); wait for it to finish" workId)
            )
        else
            runPendingAsk gitCommonDir store workId activeId intent delegateTo

    let executeAsk
        (gitCommonDir: string)
        (store: IEventStore)
        (workId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        match lifecycleWorkOf gitCommonDir workId with
        | None -> Task.FromResult(Error "lifecycle work is missing")
        | Some work ->
            match validateAskWork work intent with
            | Error err -> Task.FromResult(Error err)
            | Ok() -> beginPendingAsk gitCommonDir store workId (Option.defaultValue "" work.ActiveId) intent delegateTo

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
