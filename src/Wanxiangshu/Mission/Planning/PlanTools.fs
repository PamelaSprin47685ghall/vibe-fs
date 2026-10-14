namespace Wanxiangshu.Mission.Planning

open System
open System.Threading.Tasks
open Wanxiangshu.Host

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

type DeliverySpawnOutcome =
    | Started of sessionId: string
    | NotStarted of reason: string
    | Unknown of reason: string

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

    /// The one lifecycle work this session owns, read from the wanxiangli
    /// journal selected by the git common directory.
    let private lifecycleWorkOf
        (gitCommonDir: string)
        (workId: string)
        : Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option =
        if String.IsNullOrWhiteSpace gitCommonDir then
            None
        else
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId

    let private requireMatchingActive
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        (incumbencyId: string)
        : Result<Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork, string> =
        match work.ActiveId with
        | None -> Error "No active incumbency found for this work"
        | Some active when active <> incumbencyId ->
            Error(sprintf "Active incumbency ID '%s' does not match invocation target '%s'" active incumbencyId)
        | Some _ -> Ok work

    let private activeWorkOf
        (gitCommonDir: string)
        (workId: string)
        (incumbencyId: string)
        : Result<Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork, string> =
        let workOpt =
            if String.IsNullOrWhiteSpace gitCommonDir then
                None
            else
                Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId

        match String.IsNullOrWhiteSpace gitCommonDir, workOpt with
        | true, _ -> Error "git common directory is missing"
        | false, None -> Error "lifecycle work is missing"
        | false, Some work -> requireMatchingActive work incumbencyId

    let private draftFieldsFor (root: string) (workId: string) (hasBlocker: bool) (authorized: bool) =
        if hasBlocker || not authorized then
            []
        else
            readAndValidatePlan root workId
            |> Result.map (fun (_, digest, planPath) -> [ "draft", box {| digest = digest; path = planPath |} ])
            |> Result.defaultValue []

    let private durableObservation (gitCommonDir: string) (workId: string) (requestId: string) (command: obj) =
        let first =
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command

        if first.Status = "Unknown" then
            // Same request + same canonical command is safe to reconcile after
            // reopening the composition. No external effect is performed here.
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.execute gitCommonDir workId requestId command
        else
            first

    let private refuseDelivery
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (reason: string)
        : Result<unit, string> =
        let refused =
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                "ResolveDelivery"
                [ "operationId", box operationId
                  "outcome", box "NotStarted"
                  "reason", box reason ]

        let receipt =
            durableObservation gitCommonDir workId (operationId + ":not-started") refused

        if
            (receipt.Status = "Committed"
             || receipt.Status = "Replayed"
             || receipt.Status = "Observed")
            && receipt.Kind = "ExecutionRejected"
        then
            Ok()
        else
            let detail =
                if receipt.Reason = "" then
                    receipt.Status
                else
                    receipt.Reason

            Error(sprintf "delivery rejection settlement failed: %s" detail)

    let private bindExecutionSession
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (execSessionId: string)
        : Result<unit, string> =
        let command =
            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.command
                "ResolveDelivery"
                [ "operationId", box operationId
                  "outcome", box "SessionCreated"
                  "execSessionId", box execSessionId ]

        let receipt =
            durableObservation gitCommonDir workId (operationId + ":session-created") command

        if
            (receipt.Status = "Committed"
             || receipt.Status = "Replayed"
             || receipt.Status = "Observed")
            && receipt.Kind = "ExecutionSessionBound"
            && Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "execSessionId" = execSessionId
        then
            Ok()
        else
            Error(
                sprintf
                    "execution session binding was not durably confirmed: %s"
                    (if receipt.Reason = "" then
                         receipt.Status
                     else
                         receipt.Reason)
            )

    let private rereadExecutionBinding gitCommonDir workId operationId execSessionId original =
        let read () =
            match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
            | Some work when
                work.PendingOperationId = Some operationId
                && work.PendingExecSessionId = Some execSessionId
                ->
                Ok()
            | Some work when work.PendingExecSessionId.IsSome ->
                Error "a different execution session is already bound to this delivery"
            | _ -> Error original

        try
            read ()
        with _ ->
            Error original

    let bindObservedExecutionSession
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (execSessionId: string)
        : Result<unit, string> =
        match bindExecutionSession gitCommonDir workId operationId execSessionId with
        | Ok() -> Ok()
        | Error original -> rereadExecutionBinding gitCommonDir workId operationId execSessionId original

    let private receiptRefusal (receipt: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostReceipt) =
        if receipt.Reason = "" then
            receipt.Status
        else
            receipt.Reason

    let private settledDelivery
        (resolved: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostReceipt)
        (digest: string)
        (path: string)
        (execSessionId: string)
        : Result<DeliverResult, string> =
        if resolved.Status <> "Committed" && resolved.Status <> "Replayed" then
            Error(receiptRefusal resolved)
        elif resolved.Kind <> "Delivered" then
            Error(sprintf "delivery acceptance did not settle as Delivered: %s" resolved.Kind)
        else
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

    let private settleAcceptedDelivery
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (execSessionId: string)
        (digest: string)
        (path: string)
        (blockers: string array)
        : Result<DeliverResult, string> =
        if not (Array.isEmpty blockers) then
            Error "Execution was accepted, but live resources still block Plan retirement"
        else
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
                durableObservation gitCommonDir workId (operationId + ":accepted") accepted

            settledDelivery resolved digest path execSessionId

    let private rejectedDelivery gitCommonDir workId operationId reason : Result<DeliverResult, string> =
        refuseDelivery gitCommonDir workId operationId reason
        |> Result.bind (fun () -> Error reason)

    let private discardBeforeReject
        (discardSession: string -> Task<Result<unit, string>>)
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (execSessionId: string)
        (reason: string)
        : Task<Result<DeliverResult, string>> =
        task {
            match! discardSession execSessionId with
            | Error cleanup ->
                return
                    Error(
                        sprintf "Execution prompt was refused, but its empty session could not be discarded: %s" cleanup
                    )
            | Ok() -> return rejectedDelivery gitCommonDir workId operationId reason
        }

    let private promptBoundDelivery
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (execSessionId: string)
        (digest: string)
        (path: string)
        (retirementBlockers: unit -> string array)
        (promptSession: string -> string -> string -> Task<DeliverySpawnOutcome>)
        (discardSession: string -> Task<Result<unit, string>>)
        (content: string)
        : Task<Result<DeliverResult, string>> =
        task {
            match! promptSession execSessionId "manager" content with
            | NotStarted err ->
                return! discardBeforeReject discardSession gitCommonDir workId operationId execSessionId err
            | Unknown err -> return Error(sprintf "Execution session prompt outcome is unknown: %s" err)
            | Started observedSession when observedSession <> execSessionId ->
                return Error "Execution prompt returned a different session identity"
            | Started _ ->
                return
                    settleAcceptedDelivery
                        gitCommonDir
                        workId
                        operationId
                        execSessionId
                        digest
                        path
                        (retirementBlockers ())
        }

    let private createBindAndPrompt
        (gitCommonDir: string)
        (workId: string)
        (operationId: string)
        (digest: string)
        (path: string)
        (retirementBlockers: unit -> string array)
        (createSession: string -> string -> Task<Result<string, string>>)
        (promptSession: string -> string -> string -> Task<DeliverySpawnOutcome>)
        (discardSession: string -> Task<Result<unit, string>>)
        (content: string)
        : Task<Result<DeliverResult, string>> =
        task {
            let! created = createSession operationId "manager"

            let bound =
                created
                |> Result.bind (fun sessionId ->
                    bindObservedExecutionSession gitCommonDir workId operationId sessionId
                    |> Result.map (fun () -> sessionId))

            match bound with
            | Error reason -> return Error reason
            | Ok execSessionId ->
                return!
                    promptBoundDelivery
                        gitCommonDir
                        workId
                        operationId
                        execSessionId
                        digest
                        path
                        retirementBlockers
                        promptSession
                        discardSession
                        content
        }

    let private resolveDeliveryRequest
        (gitCommonDir: string)
        (root: string)
        (workId: string)
        (operationId: string)
        (digest: string)
        (path: string)
        (retirementBlockers: unit -> string array)
        (createSession: string -> string -> Task<Result<string, string>>)
        (promptSession: string -> string -> string -> Task<DeliverySpawnOutcome>)
        (discardSession: string -> Task<Result<unit, string>>)
        : Task<Result<DeliverResult, string>> =
        task {
            match readAndValidatePlan root workId with
            | Error err -> return rejectedDelivery gitCommonDir workId operationId err
            | Ok(content, currentDigest, currentPath) when currentDigest = digest && currentPath = path ->
                return!
                    createBindAndPrompt
                        gitCommonDir
                        workId
                        operationId
                        digest
                        path
                        retirementBlockers
                        createSession
                        promptSession
                        discardSession
                        content
            | Ok _ ->
                let error = "Cannot deliver: plan artifact changed after delivery reservation"

                return rejectedDelivery gitCommonDir workId operationId error
        }

    let private interpretDeliverReceipt
        (gitCommonDir: string)
        (root: string)
        (workId: string)
        (retirementBlockers: unit -> string array)
        (createSession: string -> string -> Task<Result<string, string>>)
        (promptSession: string -> string -> string -> Task<DeliverySpawnOutcome>)
        (discardSession: string -> Task<Result<unit, string>>)
        (receipt: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.HostReceipt)
        : Task<Result<DeliverResult, string>> =
        task {
            if
                receipt.Status = "Unknown"
                || receipt.Status = "NotWritten"
                || receipt.Status = "Conflict"
            then
                return
                    Error(
                        if receipt.Reason = "" then
                            receipt.Status
                        else
                            receipt.Reason
                    )
            elif receipt.Kind = "ReviewRequired" then
                return Error "assessment required"
            elif receipt.Status = "Committed" && receipt.Kind = "DeliveryRequested" then
                let operationId =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "operationId"

                let digest =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "digest"

                let path = Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "path"

                return!
                    resolveDeliveryRequest
                        gitCommonDir
                        root
                        workId
                        operationId
                        digest
                        path
                        retirementBlockers
                        createSession
                        promptSession
                        discardSession
            elif receipt.Kind = "ObserveExecution" || receipt.Kind = "ExecutionSessionBound" then
                let operationId =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "operationId"

                return
                    Error(
                        if operationId = "" then
                            "Execution delivery remains pending; observe the existing operation"
                        else
                            sprintf "Execution delivery remains pending; observe operation %s" operationId
                    )
            elif receipt.Kind = "ExecutionRejected" then
                let reason =
                    Wanxiangshu.Mission.Lifecycle.DurableWorkHost.resultText receipt "reason"

                return
                    Error(
                        if reason = "" then
                            "Execution delivery was confirmed not started"
                        else
                            reason
                    )
            elif
                receipt.Kind = "Relayed"
                || receipt.Kind = "Blocked"
                || receipt.Kind = "Delivered"
            then
                return
                    Ok
                        { Ok = true
                          Relayed = receipt.Kind = "Relayed"
                          Delivered = receipt.Kind = "Delivered"
                          RetiredIncumbencyId = ""
                          RetirementOutcome = receipt.Kind
                          Digest = ""
                          Path = ""
                          ExecSessionId = ""
                          NextIncumbencyId = ""
                          Message = receipt.Kind }
            else
                return Error(sprintf "Unexpected lifecycle deliver result: %s/%s" receipt.Status receipt.Kind)
        }

    let executeDeliver
        (gitCommonDir: string)
        (root: string)
        (workId: string)
        (incumbencyId: string)
        (providerRunId: string)
        (toolCallId: string)
        (retirementBlockers: unit -> string array)
        (createSession: string -> string -> Task<Result<string, string>>)
        (promptSession: string -> string -> string -> Task<DeliverySpawnOutcome>)
        (discardSession: string -> Task<Result<unit, string>>)
        (retirementCursor: int64)
        : Task<Result<DeliverResult, string>> =
        task {
            match activeWorkOf gitCommonDir workId incumbencyId with
            | Error err -> return Error err
            | Ok work ->
                let authorized = work.DeliveryAuthorized
                let requestId = providerRunId + ":" + toolCallId
                let blockers = retirementBlockers ()

                let draftFields =
                    draftFieldsFor root workId (not (Array.isEmpty blockers)) authorized

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

                return!
                    interpretDeliverReceipt
                        gitCommonDir
                        root
                        workId
                        retirementBlockers
                        createSession
                        promptSession
                        discardSession
                        receipt
        }

    let private pendingExecutionEvidence
        (execSessionId: string)
        (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork)
        =
        match work.PendingOperationId, work.PendingExecSessionId, work.PendingDigest, work.PendingPath with
        | Some operationId, Some boundSession, Some digest, Some path when boundSession = execSessionId ->
            Ok(operationId, digest, path)
        | Some _, None, _, _ -> Error "delivery session is not durably bound"
        | _ -> Error "pending delivery evidence is incomplete"

    /// Crash reconciliation for a session that was durably bound before the
    /// process died. The Host supplies the exact observed first user prompt;
    /// this function verifies both session identity and plan bytes before
    /// settling the original delivery operation. It never sends or creates.
    let reconcileBoundExecution
        (gitCommonDir: string)
        (workId: string)
        (execSessionId: string)
        (observedPrompt: string)
        (blockers: string array)
        : Result<DeliverResult, string> =
        let evidence =
            match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir workId with
            | None -> Error "lifecycle work is missing"
            | Some work when not work.PendingDelivery -> Error "delivery operation is not pending"
            | Some work -> pendingExecutionEvidence execSessionId work

        evidence
        |> Result.bind (fun (operationId, digest, path) ->
            if HostDigest.sha256Hex observedPrompt <> digest then
                Error "observed execution prompt does not match the reserved plan digest"
            else
                settleAcceptedDelivery gitCommonDir workId operationId execSessionId digest path blockers)

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
        (workId: string)
        (incumbencyId: string)
        : Task<Result<unit, string>> =
        task {
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

            if
                receipt.Status = "Committed"
                || receipt.Status = "Replayed"
                || receipt.Status = "Observed"
            then
                return Ok()
            else
                return
                    Error(
                        sprintf
                            "Failed to bind plan DevOps: %s"
                            (if receipt.Reason = "" then
                                 receipt.Status
                             else
                                 receipt.Reason)
                    )
        }

    let private bindDevOpsAndComplete
        (gitCommonDir: string)
        (workId: string)
        (activeId: string)
        (record: string)
        : Task<Result<AskResult, string>> =
        task {
            match! ensureDevOpsBound gitCommonDir workId activeId with
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
        (workId: string)
        (activeId: string)
        (delegation: Result<string, string>)
        : Task<Result<AskResult, string>> =
        task {
            match delegation with
            | Error err -> return Error err
            | Ok record when String.IsNullOrWhiteSpace record ->
                return Error "The DevOps delegation returned an empty work record"
            | Ok record -> return! bindDevOpsAndComplete gitCommonDir workId activeId record
        }

    let private runPendingAsk
        (gitCommonDir: string)
        (workId: string)
        (activeId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        task {
            try
                let! delegation = captureDelegation delegateTo intent
                let! completed = finishPendingAsk gitCommonDir workId activeId delegation
                return completed
            finally
                clearPendingAsk workId
        }

    let private beginPendingAsk
        (gitCommonDir: string)
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
            runPendingAsk gitCommonDir workId activeId intent delegateTo

    let executeAsk
        (gitCommonDir: string)
        (workId: string)
        (intent: string)
        (delegateTo: string -> Task<string>)
        : Task<Result<AskResult, string>> =
        let validated =
            match lifecycleWorkOf gitCommonDir workId with
            | None -> Error "lifecycle work is missing"
            | Some work -> validateAskWork work intent |> Result.map (fun () -> work)

        match validated with
        | Error err -> Task.FromResult(Error err)
        | Ok work -> beginPendingAsk gitCommonDir workId (Option.defaultValue "" work.ActiveId) intent delegateTo

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
