namespace Wanxiangshu.OpenCode

open Wanxiangshu.Persistence.Journal.JournalOutcome
#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Git
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Work
open Wanxiangshu.Mission.Work.OpenCode
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.OpenCode.Host
open PluginHostInterop

module PluginHooks =

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasOwnProperty (target: obj) (prop: string) : bool = jsNative

    [<Emit("$0 !== null && $0 !== undefined && typeof $0 === 'object' && !Array.isArray($0) && Object.keys($0).length > 0")>]
    let private isNonEmptyObject (value: obj) : bool = jsNative

    /// Host hook surface: chat / transform / config / compaction / text /
    /// tool hooks plus event + dispose, and the optional client tool module.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) (transform: obj -> obj -> Task<unit>) : Task<obj> =
        task {
            let scope = boot.Scope
            let journal = boot.Journal
            let wired = host.Wired
            let workspaceDirectory = boot.WorkspaceDirectory
            let input = boot.Input
            let sessionPort = host.SessionPort
            let snapshotOpt = host.SnapshotOpt
            let eventPort = host.EventPort
            let chatParams = ChatParamsHook.createWith journal

            let roleFor (sessionId: SessionId) =
                journal
                |> Option.bind (fun durable ->
                    let projections = (AgentJournal.snapshot durable).AgentProjections

                    PromptAuthorityProjectionQueries.activeProfile sessionId projections
                    |> Option.orElseWith (fun () ->
                        PromptAuthorityProjectionQueries.lastAuthorityProfile sessionId projections))
                |> Option.map (fun profile -> profile.CanonicalRole)

            let isReplica (sessionId: SessionId) =
                boot.StrengthScope.StrengthRuntime.TryFindByReplica sessionId |> Option.isSome

            // work-031: resolve the stage-method semantic path for the
            // session's active plan incumbency. Non-plan sessions, missing
            // stores and delivered works resolve None and append nothing, so
            // the non-plan system prompt path stays byte-identical.
            let stagePathFor (stage: string) (activeId: string option) : string option =
                match stage, activeId with
                | "S1", Some _ -> Some "planning/s1"
                | "S2", Some _ -> Some "planning/s2"
                | "S3", Some _ -> Some "planning/s3"
                | _ -> None

            let planStageFor (sessionId: SessionId) : string option =
                match roleFor sessionId with
                | Some Role.Plan ->
                    workspaceDirectory
                    |> Option.bind (fun workspace ->
                        let commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot workspace

                        Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected commonDir (SessionId.value sessionId))
                    |> Option.bind (fun work -> stagePathFor work.Stage work.ActiveId)
                | _ -> None

            let systemTransform =
                ProviderSystemTransform.createWith roleFor isReplica planStageFor

            // CASE-003: typed capture at the tool boundary — shared
            // CasebookLifecycle.collector; marker flag gates the after-hook.
            // Store IO stays out of SpikePlugin (unified-store dual-write gate).
            let casebookEnabled =
                match workspaceDirectory with
                | Some ws -> CasebookFeature.isEnabled ws
                | None -> false

            // Programming-surface calls carrying a non-empty `view` argument establish
            // the compression checkpoint. The plugin records one durable compression
            // checkpoint per successfully completed call, keyed by exact call id.
            let settledViewCheckpoints = HashSet<SessionId * ToolCallId>()
            // obligation-ledger-005: the in-flight append for each identity, so a
            // concurrent duplicate terminal waits for the same append's final
            // outcome instead of reporting an early success.
            let pendingViewCheckpointAppends = Dictionary<SessionId * ToolCallId, Task<unit>>()

            let collectCasebookObservation (toolInput: obj) (toolOutput: obj) =
                let toolName = if isNull toolInput then "" else string (toolInput?tool)

                let sessionId =
                    if isNull toolInput then
                        ""
                    else
                        string (toolInput?sessionID)

                let rendered = if isNull toolOutput then "" else string (toolOutput?output)

                if not (System.String.IsNullOrWhiteSpace sessionId) then
                    CasebookLifecycle.collector.Collect(sessionId, toolName, toolInput?args, rendered)

            let ownedTransform (inObj: obj) (outObj: obj) : Task =
                scope.RunOwnedWork(fun () -> transform inObj outObj)

            let client = if isNull input then null else input?client

            // concern-routing-003: the reserved `user` address renders as a
            // user-visible TUI notification. The server SDK exposes
            // `client.tui.showToast`; a missing client leaves the notifier
            // absent and publish still copies to `root`.
            let userNotify =
                if isNull client then
                    None
                else
                    Some(fun (title: string) (message: string) ->
                        Fable.Core.JsInterop.emitJsExpr
                            (client, title, message)
                            "$0?.tui?.showToast?.({ body: { title: $1, message: $2, variant: 'info' } })"
                        |> ignore)

            let isToastAvailable () : bool =
                emitJsExpr client "typeof $0?.tui?.showToast === 'function'"

            let checkQuestionToastResult (response: obj) : Result<unit, string> =
                if not (isNull response) && not (isNull response?error) then
                    Error(string response?error)
                else
                    Ok()

            let postQuestionToastUnchecked (question: string) : Task<Result<unit, string>> =
                task {
                    let title =
                        ProviderProse.render
                            (ProviderLanguageBinding.readGlobalPreference ())
                            "tool/ask-title"
                            Map.empty

                    let! response =
                        emitJsExpr
                            (client, title, question)
                            "$0.tui.showToast({ body: { title: $1, message: $2, variant: 'info' } })"
                        |> unbox<Task<obj>>

                    return checkQuestionToastResult response
                }

            let runGuardedToast (question: string) : Task<Result<unit, string>> =
                task {
                    try
                        return! postQuestionToastUnchecked question
                    with ex ->
                        return Error ex.Message
                }

            let showQuestionToast (question: string) : Task<Result<unit, string>> =
                if not (isToastAvailable ()) then
                    Task.FromResult(Error "the Host cannot present a question to the user")
                else
                    runGuardedToast question

            let configureClient () : Task<ToolRegistration> =
                task {
                    let! toolModule = importToolModule ()

                    let onRunStarted =
                        Some(fun sessionId role directory -> wired.BindActiveRun sessionId role directory)

                    // EXEC-006/008: LWR; parent→child Opening on, join off.
                    let workRecord includeOpening =
                        Some(fun sessionId ->
                            LifecycleWorkRecordProjection.lifecycleWorkRecord
                                journal
                                (SessionId.create sessionId)
                                includeOpening)

                    let parentWorkRecordFor, childWorkRecordFor = workRecord true, workRecord false

                    let casebookToolSpecs: ToolSpec list =
                        match workspaceDirectory with
                        | Some ws ->
                            CasebookTools.buildSpecs (ToolHostCodec.factory toolModule) ws boot.CasebookSettlements
                        | None -> []

                    let toolRegistration =
                        toolHooks
                            toolModule
                            sessionPort
                            host.CausalWaitObserver
                            host.RootWorkspace
                            journal
                            (workspaceDirectory)
                            (Some boot.StrengthScope)
                            scope
                            wired.CurrentPhysicalUserMessage
                            onRunStarted
                            parentWorkRecordFor
                            childWorkRecordFor
                            snapshotOpt
                            (Some wired.CancelSignals)
                            (Some eventPort)
                            casebookToolSpecs
                            (fun (managerSessionId: SessionId) (managerWorkspace: string) ->
                                task {
                                    try
                                        do!
                                            ManagerWorkflow.maybeDeliverLoop
                                                sessionPort
                                                host.RootWorkspace
                                                journal
                                                (Some managerWorkspace)
                                                (Some(SessionId.value managerSessionId))

                                        return Ok()
                                    with ex ->
                                        return Error ex.Message
                                })
                            userNotify

                    toolRegistration.Runtime.AttachQuestionNotifier(fun question -> showQuestionToast question)

                    scope.AttachToolRuntime(toolRegistration.Runtime :> ISessionRuntimeOwner)

                    return toolRegistration
                }

            let guardedClientConfiguration () : Task<ToolRegistration> =
                task {
                    try
                        return! configureClient ()
                    with ex ->
                        return
                            raise (
                                InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message)
                            )
                }

            let! toolRegistration =
                if isNull client then
                    Task.FromResult None
                else
                    task {
                        let! registration = guardedClientConfiguration ()
                        return Some registration
                    }

            let getManagerCapabilityFacts (sessionId: string) =
                match toolRegistration with
                | Some registration -> registration.Runtime.ManagerCapabilityFactsFor sessionId
                | None -> ToolRuntimeScope.emptyManagerFacts

            // the only enablement condition for explicit
            // read-only delegation is that a Predictor model is configured.
            // The read-only configuration existence query is owned by
            // ModelRouting (ModelRouting.sharedPredictorConfiguration, loaded
            // once together with the sole MJS model configuration during the
            // PluginBoot Load Phase, before any tool definition is registered
            // or presented) and is shared by tool decoration and delegation
            // admission, so this hook consumes the query instead of holding a
            // second enabled truth. Configured decorates the schema and
            // appends the collaboration prose; NotConfigured decorates nothing.
            // ConfigurationInvalid is a malformed model configuration: it fails
            // closed here rather than silently degrading to "not configured",
            // the same choice as ModelRouting.requireRoutingProtocol; the
            // hook's registered disposition (HookPolicy ToolDefinition:
            // Invariant / TypedPolicyFailClosed, diagnostic operation
            // plugin-hook-tool-definition-failed) already carries the report.
            let readonlyDelegationPredictorConfigured () : bool =
                match ModelRouting.sharedPredictorConfiguration () with
                | ModelRouting.PredictorConfiguration.Configured -> true
                | ModelRouting.PredictorConfiguration.NotConfigured -> false
                | ModelRouting.PredictorConfiguration.ConfigurationInvalid reason ->
                    raise (
                        InvalidOperationException(
                            sprintf "execution-model-routing: Predictor model configuration is invalid: %s" reason
                        )
                    )

            let toolDefinition (toolInput: obj) (toolOutput: obj) =
                ManagerReviewContract.decorateDefinition toolInput toolOutput

                if readonlyDelegationPredictorConfigured () then
                    ReadonlyDelegationContract.decorateDefinition toolInput toolOutput

            let reviewToolPermitted toolName (facts: ManagerCapabilityFacts) =
                match ManagerReviewTools.requiredPermissions toolName with
                | Some required ->
                    let allowed = OfficeCapability.permissionsForManagerFacts facts
                    Set.isSubset required allowed && not (Set.isEmpty allowed)
                | None -> false

            let isReviewPermitted toolName (facts: ManagerCapabilityFacts) =
                if toolName = "todowrite-for-manager" && facts.HasActiveIncumbency then
                    OfficeCapability.isAllowedForManagerFacts facts ToolPermission.Read
                else
                    reviewToolPermitted toolName facts

            let assertReviewPermitted toolName sessionId =
                let facts = getManagerCapabilityFacts sessionId

                if not (isReviewPermitted toolName facts) then
                    invalidOp (
                        sprintf
                            "Manager review tool '%s' is not permitted under current manager capability facts"
                            toolName
                    )

            let toolField (toolInput: obj) (name: string) =
                if isNull toolInput || isNull toolInput?(name) then
                    ""
                else
                    string toolInput?(name)

            let appendViewCheckpoint durable sessionText callId =
                task {
                    let fact =
                        ContextFact.ViewCheckpointCommitted
                            {| SessionId = SessionId.create sessionText
                               ToolCallId = callId |}

                    match!
                        AgentJournal.appendAgent (StreamId.Session(SessionId.create sessionText)) None fact durable
                    with
                    | Ok _ -> ()
                    | Error failure -> raise (JournalAppendException failure)
                }

            let eventText (value: obj) =
                if isNull value then
                    None
                else
                    let text = string value
                    if String.IsNullOrWhiteSpace text then None else Some text

            let firstFieldText (carrier: obj) names =
                if isNull carrier then
                    None
                else
                    names |> List.tryPick (fun name -> eventText carrier?(name))

            let toolTerminalObservation expectedTool rawInput =
                let raw = HostEventEnvelope.unwrap rawInput
                let properties = if isNull raw then null else raw?properties
                let part = if isNull properties then null else properties?part
                let state = if isNull part then null else part?state

                let sessionId =
                    HostEventEnvelope.trySessionId raw
                    |> Option.orElseWith (fun () ->
                        firstFieldText part [ "sessionID"; "sessionId" ] |> Option.map SessionId.create)

                let tool = firstFieldText part [ "tool"; "name" ]
                let callId = firstFieldText part [ "callID"; "callId"; "toolCallId" ]

                let status =
                    firstFieldText state [ "status" ]
                    |> Option.map (fun value -> value.ToLowerInvariant())

                match HostEventEnvelope.eventTypeOf raw, sessionId, tool, callId, status with
                | "message.part.updated", Some sessionId, Some tool, Some callId, Some status when
                    String.Equals(tool, expectedTool, StringComparison.OrdinalIgnoreCase)
                    && (status = "completed" || status = "error")
                    ->
                    let providerRun =
                        firstFieldText part [ "messageID"; "messageId" ]
                        |> Option.map ProviderRunIdentity.create

                    Some(SessionId.value sessionId, ToolCallId.create callId, status, providerRun)
                | _ -> None

            let viewTerminalObservation rawInput =
                let raw = HostEventEnvelope.unwrap rawInput
                let properties = if isNull raw then null else raw?properties
                let part = if isNull properties then null else properties?part
                let state = if isNull part then null else part?state
                let input = if isNull state then null else state?input
                let view = if isNull input then null else input?view

                let sessionId =
                    HostEventEnvelope.trySessionId raw
                    |> Option.orElseWith (fun () ->
                        firstFieldText part [ "sessionID"; "sessionId" ] |> Option.map SessionId.create)

                let tool = firstFieldText part [ "tool"; "name" ]
                let callId = firstFieldText part [ "callID"; "callId"; "toolCallId" ]

                let status =
                    firstFieldText state [ "status" ]
                    |> Option.map (fun value -> value.ToLowerInvariant())

                match HostEventEnvelope.eventTypeOf raw, sessionId, tool, callId, status with
                | "message.part.updated", Some sessionId, Some tool, Some callId, Some status when
                    tool.StartsWith("todowrite-for-", StringComparison.OrdinalIgnoreCase)
                    && (status = "completed" || status = "error")
                    && isNonEmptyObject view
                    ->
                    Some(SessionId.value sessionId, ToolCallId.create callId, status)
                | _ -> None

            let requireUserQuestionInterrupted (result: Result<unit, string>) =
                match result with
                | Ok() -> ()
                | Error reason -> invalidOp ("ask was sent, but its attempt interrupt failed: " + reason)

            let settleUserQuestion rawInput =
                task {
                    // `js.ask` inside a plan program records the same durable
                    // question under the plan tool's call id, so both the
                    // native ask tool and the plan programming tool complete
                    // as the interrupt boundary.
                    let observation =
                        toolTerminalObservation "ask" rawInput
                        |> Option.orElseWith (fun () ->
                            toolTerminalObservation
                                (Wanxiangshu.Repository.Programming.Js.JsToolGenerator.toolNameFor "plan")
                                rawInput)

                    match observation, toolRegistration with
                    | Some(sessionId, callId, "completed", Some providerRun), Some registration ->
                        // completed 工具部件是占位回执已被 Host 接受的因果边界；不靠定时器猜。
                        let! result =
                            registration.Runtime.InterruptForUserQuestion(
                                SessionId.create sessionId,
                                providerRun,
                                callId
                            )

                        requireUserQuestionInterrupted result
                    | Some(sessionId, _, "completed", None), Some registration when
                        registration.Runtime.IsWaitingForUser sessionId
                        ->
                        invalidOp
                            "ask result is missing the exact provider message identity; no attempt was interrupted"
                    | _ -> ()
                }

            let requirePostS3Interrupted (result: Result<unit, string>) =
                match result with
                | Ok() -> ()
                | Error reason ->
                    invalidOp ("post-S3 deliver was recorded, but its attempt interrupt failed: " + reason)

            let settlePostS3Deliver rawInput =
                task {
                    match toolTerminalObservation "deliver" rawInput, toolRegistration with
                    | Some(sessionId, callId, "completed", Some providerRun), Some registration ->
                        let! result =
                            registration.Runtime.InterruptForPostS3Deliver(
                                SessionId.create sessionId,
                                providerRun,
                                callId
                            )

                        requirePostS3Interrupted result
                    | Some(sessionId, _, "completed", None), Some registration when
                        registration.Runtime.HasPendingPostS3Interrupt sessionId
                        ->
                        invalidOp
                            "post-S3 deliver result is missing the exact provider message identity; no attempt was interrupted"
                    | _ -> ()
                }

            let requiredViewJournal () =
                journal
                |> Option.defaultWith (fun () -> invalidOp "view compression checkpoint requires a durable journal")

            let removePendingViewCheckpoint key =
                lock pendingViewCheckpointAppends (fun () -> pendingViewCheckpointAppends.Remove(key) |> ignore)

            let releaseUnattemptedViewCheckpoint key failure =
                match failure with
                | JournalAppendFailure.WriterUnavailable _ ->
                    removePendingViewCheckpoint key
                    settledViewCheckpoints.Remove(key) |> ignore
                | JournalAppendFailure.WriteUnknown _
                | JournalAppendFailure.NoNewWriteReleaseFailed _
                | JournalAppendFailure.FactRejected _ -> ()

            let appendTerminalViewCheckpoint key sessionText callId =
                task {
                    try
                        do! appendViewCheckpoint (requiredViewJournal ()) sessionText callId
                        removePendingViewCheckpoint key
                    with :? JournalAppendException as appendFailure ->
                        releaseUnattemptedViewCheckpoint key appendFailure.Failure
                        return raise appendFailure
                }

            let startTerminalViewCheckpoint key sessionText callId =
                lock pendingViewCheckpointAppends (fun () ->
                    match pendingViewCheckpointAppends.TryGetValue(key) with
                    | true, pending -> pending
                    | false, _ ->
                        let append = appendTerminalViewCheckpoint key sessionText callId
                        pendingViewCheckpointAppends[key] <- append
                        append)

            let settleNewViewCheckpointTerminal key sessionText callId status =
                if not (settledViewCheckpoints.Add(key)) then
                    Task.FromResult(())
                elif status = "completed" then
                    startTerminalViewCheckpoint key sessionText callId
                else
                    Task.FromResult(())

            /// One durable checkpoint per exact terminal call. A repeated Host
            /// part update for the same call id is a replay, not a second fact.
            /// obligation-ledger-005: concurrent duplicates of the same terminal
            /// share the one in-flight append and observe its final outcome.
            /// A failed append propagates its error to every waiter. The settled
            /// mark is only kept when the outcome is final: WriteUnknown and
            /// FactRejected stay settled (the event may already be durable or is
            /// semantically refused — repeating it is unsafe), while
            /// WriterUnavailable (NotAttempted) releases the mark so the Host's
            /// retry gets a real second attempt.
            let settleViewCheckpointTerminal (sessionText, callId, status) =
                let key = SessionId.create sessionText, callId

                // A concurrent duplicate first joins the in-flight append (it
                // must observe the same final outcome); only a terminal with no
                // pending append consults the settled mark.
                let joinPending () =
                    lock pendingViewCheckpointAppends (fun () ->
                        match pendingViewCheckpointAppends.TryGetValue(key) with
                        | true, pending -> Some(pending)
                        | false, _ -> None)

                match joinPending () with
                | Some pending -> pending
                | None -> settleNewViewCheckpointTerminal key sessionText callId status

            let settleViewCheckpointEvent rawInput =
                viewTerminalObservation rawInput
                |> Option.map settleViewCheckpointTerminal
                |> Option.defaultValue (Task.FromResult(()))

            let checkManagerReviewPermissions toolName toolInput =
                let sessionId = toolField toolInput "sessionID"

                let blockedNative =
                    toolName = "todowrite-for-engineer" || toolName = "todowrite-for-devops"

                if roleFor (SessionId.create sessionId) = Some Role.Manager && blockedNative then
                    invalidOp (sprintf "Manager tool '%s' is refused on the review gate" toolName)
                elif ManagerReviewTools.isReviewTool toolName then
                    assertReviewPermitted toolName sessionId

            // host-boundary-032: snapshot the protocol
            // fields the model actually produced, before either contract family
            // hides them. The Host persists the stripped arguments, so the
            // provider transform restores the fields into the request from this
            // vault. contract only for review tools (the only place review hide
            // runs); delegation fields only for participating tools (classifyTool = EstimateAfterCall).
            //
            // The call id is read from the hook input itself: WHAT[009]'s
            // both-halves pairing lives in decodeContext, and this hook input
            // carries no messageID, so decodeContext would always answer None.
            let protocolFieldOwnership (toolName: string) : ProtocolArgumentVault.FieldOwnership =
                { ReviewContract = ManagerReviewTools.isReviewTool toolName
                  InvestigationEstimate =
                    readonlyDelegationPredictorConfigured ()
                    && InvestigationEstimateContract.classifyTool toolName = InvestigationEstimateContract.InvestigationToolPolicy.EstimateAfterCall }

            let recordSnapshotIfPresent
                (vault: ProtocolArgumentVault.Vault)
                (sessionId: string)
                (toolCallId: ToolCallId)
                (toolName: string)
                (args: obj)
                =
                match ProtocolArgumentVault.snapshotOfArguments (protocolFieldOwnership toolName) args with
                | Some snapshot -> ProtocolArgumentVault.record vault sessionId (ToolCallId.value toolCallId) snapshot
                | None -> ()

            let tryRecordVaultEntry
                (vault: ProtocolArgumentVault.Vault)
                (toolInput: obj)
                (toolOutput: obj)
                (toolCallId: ToolCallId)
                =
                let context = ToolHostCodec.decodeContext toolInput

                if not (String.IsNullOrWhiteSpace context.SessionId) then
                    let toolName = toolField toolInput "tool"
                    recordSnapshotIfPresent vault context.SessionId toolCallId toolName toolOutput?args

            let tryRecordCall
                (vault: ProtocolArgumentVault.Vault)
                (toolInput: obj)
                (toolOutput: obj)
                (callIdOpt: ToolCallId option)
                =
                match callIdOpt with
                | Some toolCallId -> tryRecordVaultEntry vault toolInput toolOutput toolCallId
                | None -> ()

            let recordProtocolArgumentVault (toolInput: obj) (toolOutput: obj) =
                if not (isNull toolOutput) && not (isNull toolOutput?args) then
                    tryRecordCall boot.ProtocolArgumentVault toolInput toolOutput (ToolHostCodec.hookCallId toolInput)

            let estimateSessionText (toolInput: obj) =
                if not (isNull toolInput) && not (isNull toolInput?sessionID) then
                    string toolInput?sessionID
                else
                    (ToolHostCodec.decodeContext toolInput).SessionId

            let rejectInvalidEstimateArguments (toolInput: obj) (args: obj) =
                match InvestigationEstimateContract.parseParticipatingArguments args with
                | Ok _ -> ()
                | Error err ->
                    let language =
                        ProviderLanguageBinding.forSessionText (estimateSessionText toolInput)

                    let explanation = InvestigationEstimateContract.formatArgumentError language err
                    invalidOp (sprintf "Invalid investigation estimate arguments: %s" explanation)

            let restoreParticipatingArguments (isParticipatingTool: bool) (args: obj) =
                if isParticipatingTool then
                    ReadonlyDelegationContract.restore args

                ManagerReviewContract.restore args

            let hideReviewArguments owner toolName args =
                if ManagerReviewTools.isReviewTool toolName then
                    ManagerReviewContract.hideForCall owner args

            let hideDelegationArguments owner isDelegationActive args =
                if isDelegationActive then
                    ReadonlyDelegationContract.hideForCall owner args

            let hideProtocolArguments owner toolName isDelegationActive args =
                try
                    hideReviewArguments owner toolName args
                    hideDelegationArguments owner isDelegationActive args
                with error ->
                    restoreParticipatingArguments isDelegationActive args
                    raise error

            let requireDelegationEstimateArguments (toolInput: obj) (toolOutput: obj) =
                if not (isNull toolOutput) && not (isNull toolOutput?args) then
                    rejectInvalidEstimateArguments toolInput toolOutput?args

            let argumentCallOwner toolName toolInput : ProtocolArgumentCall option =
                let context = ToolHostCodec.decodeContext toolInput

                match ToolHostCodec.hookCallId toolInput with
                | Some callId when not (String.IsNullOrWhiteSpace context.SessionId) ->
                    Some
                        { SessionId = SessionId.create context.SessionId
                          ToolCallId = callId
                          Tool = toolName }
                | _ -> None

            let observeDelegatedToolEstimate toolInput =
                task {
                    let context = ToolHostCodec.decodeContext toolInput

                    match journal, ToolHostCodec.hookCallId toolInput with
                    | Some durable, Some toolCallId when not (String.IsNullOrWhiteSpace context.SessionId) ->
                        let port = AgentJournalPortAdapter.forDelegatedToolEstimate durable
                        do! DelegatedToolEstimateLedger.observe port (SessionId.create context.SessionId) toolCallId
                    | _ -> ()
                }

            let hiddenReviewState owner isReview args =
                if isReview then
                    ManagerReviewContract.classifyHiddenArguments owner args
                else
                    HiddenProtocolArguments.SameCall

            let hiddenDelegationState owner isDelegationActive args =
                if isDelegationActive then
                    ReadonlyDelegationContract.classifyHiddenArguments owner args
                else
                    HiddenProtocolArguments.SameCall

            // 隐藏状态合并规则是合同：任一 Different → Different；都 Same → Same；
            // 否则 NotHidden。并发与重放下同一参数对象只有一个隐藏归属
            //（host-boundary-032）。
            let combineHiddenStates review delegation =
                match review, delegation with
                | HiddenProtocolArguments.DifferentCallOrChangedArguments, _
                | _, HiddenProtocolArguments.DifferentCallOrChangedArguments ->
                    HiddenProtocolArguments.DifferentCallOrChangedArguments
                | HiddenProtocolArguments.SameCall, HiddenProtocolArguments.SameCall -> HiddenProtocolArguments.SameCall
                | _ -> HiddenProtocolArguments.NotHidden

            let hiddenArgumentState owner toolName isDelegationActive (toolOutput: obj) =
                let isReview = ManagerReviewTools.isReviewTool toolName

                if
                    (not isReview && not isDelegationActive)
                    || isNull toolOutput
                    || isNull toolOutput?args
                then
                    HiddenProtocolArguments.NotHidden
                else
                    combineHiddenStates
                        (hiddenReviewState owner isReview toolOutput?args)
                        (hiddenDelegationState owner isDelegationActive toolOutput?args)

            let protocolArgumentsAreHidden owner toolName isDelegationActive toolOutput =
                match hiddenArgumentState owner toolName isDelegationActive toolOutput with
                | HiddenProtocolArguments.SameCall -> true
                | HiddenProtocolArguments.DifferentCallOrChangedArguments ->
                    invalidOp
                        "Invalid investigation estimate arguments: hidden arguments belong to another call or were changed before restoration"
                | HiddenProtocolArguments.NotHidden -> false

            // 隐藏前再核一次：本调用尚未隐藏且 args 可读才执行 hide。
            // hideProtocolArguments 半途失败时补偿性全量 restore 再重抛，是 before
            // 失败的唯一回滚点（宿主不会在 before 失败后再调 after）。
            let hideProtocolArgumentsIfShown owner toolName isDelegationActive (toolOutput: obj) =
                if
                    not (protocolArgumentsAreHidden owner toolName isDelegationActive toolOutput)
                    && not (isNull toolOutput)
                    && not (isNull toolOutput?args)
                then
                    hideProtocolArguments owner toolName isDelegationActive toolOutput?args

            // 新参数准备链的具名步骤，按序：非法估计拒绝 → wire vault 记录 →
            // 委托估计观察 → 隐藏协议字段。
            let prepareNewProtocolArguments owner toolName isDelegationActive toolInput toolOutput =
                task {
                    if isDelegationActive then
                        requireDelegationEstimateArguments toolInput toolOutput

                    recordProtocolArgumentVault toolInput toolOutput
                    do! observeDelegatedToolEstimate toolInput
                    hideProtocolArgumentsIfShown owner toolName isDelegationActive toolOutput
                }

            // 已隐藏同调用 → 短路：不重复记录、不重复隐藏。不同调用或参数已变
            // 由 protocolArgumentsAreHidden 报错。
            let beforeStagePrepareProtocolArguments toolName isDelegationActive toolInput toolOutput =
                task {
                    let owner = argumentCallOwner toolName toolInput

                    if not (protocolArgumentsAreHidden owner toolName isDelegationActive toolOutput) then
                        do! prepareNewProtocolArguments owner toolName isDelegationActive toolInput toolOutput
                }

            // tool.execute.before 的既有 stage 序列。work-049 的显式用户等待在这些
            // 工作步骤之前拒绝新动作；未等待时仍先 grounding，再评审和参数准备。
            // 第 2 步只对评审工具生效；第 3/4 步
            // 服务参与工具的估计协议。任何一步抛错即中断本次 before。
            let beforeStageRequirementGrounding (toolInput: obj) (toolOutput: obj) =
                Wanxiangshu.OpenCode.Host.RequirementGrounding.RequirementGroundingGate.before
                    journal
                    workspaceDirectory
                    toolInput
                    toolOutput

            let beforeStageReviewPermission (toolName: string) (toolInput: obj) =
                checkManagerReviewPermissions toolName toolInput

            let beforeStageDelegationActive toolName =
                let isParticipatingTool =
                    InvestigationEstimateContract.classifyTool toolName = InvestigationEstimateContract.InvestigationToolPolicy.EstimateAfterCall

                readonlyDelegationPredictorConfigured () && isParticipatingTool

            let toolBefore (toolInput: obj) (toolOutput: obj) =
                task {
                    let sessionId = toolField toolInput "sessionID"
                    let toolName = toolField toolInput "tool"

                    // capability-enforcement-002/007: a Host-declared external
                    // (MCP) tool holds no office capability, so it is advertised
                    // but fails closed here, before any stage.
                    match toolRegistration with
                    | Some registration -> do! ExternalToolAdmission.before registration.Runtime sessionId toolName
                    | None -> ()

                    match toolRegistration with
                    | Some registration when toolName <> "ask" && registration.Runtime.IsWaitingForUser sessionId ->
                        invalidOp (
                            ProviderProse.render
                                (ProviderLanguageBinding.forSessionText sessionId)
                                "tool/ask-waiting"
                                Map.empty
                        )
                    | _ -> ()

                    do! beforeStageRequirementGrounding toolInput toolOutput
                    beforeStageReviewPermission toolName toolInput

                    do!
                        beforeStagePrepareProtocolArguments
                            toolName
                            (beforeStageDelegationActive toolName)
                            toolInput
                            toolOutput
                }

            // 先 Delegation（仅参与工具）后 Manager，两个字段族互不覆盖。
            let restoreOwnedArguments owner isParticipatingTool args =
                if isParticipatingTool then
                    ReadonlyDelegationContract.restoreForCall owner args

                ManagerReviewContract.restoreForCall owner args

            let restoreArgumentsForTarget owner isParticipatingTool (target: obj) =
                if not (isNull target) && not (isNull target?args) then
                    restoreOwnedArguments owner isParticipatingTool target?args

            // after stage 1: 按 exact 调用身份重算 classify/owner，对 toolInput 与
            // toolOutput 各恢复一次协议字段。restore 抛错则后续 stage 不执行（现状）。
            let afterStageRestoreProtocolArguments (toolInput: obj) (toolOutput: obj) =
                let toolName = toolField toolInput "tool"
                let owner = argumentCallOwner toolName toolInput

                let isParticipatingTool =
                    InvestigationEstimateContract.classifyTool toolName = InvestigationEstimateContract.InvestigationToolPolicy.EstimateAfterCall

                restoreArgumentsForTarget owner isParticipatingTool toolInput
                restoreArgumentsForTarget owner isParticipatingTool toolOutput

            // after stage 2: requirement grounding 读结果补规范。
            let afterStageRequirementGrounding (toolInput: obj) (toolOutput: obj) =
                Wanxiangshu.OpenCode.Host.RequirementGrounding.RequirementGroundingGate.after
                    journal
                    workspaceDirectory
                    toolInput
                    toolOutput

            // after stage 3: 可选 casebook 观察。失败只发诊断，不改关键结果
            //（host-boundary-024）。
            let afterStageCasebookObservation (toolInput: obj) (toolOutput: obj) =
                if casebookEnabled then
                    HookPolicy.observeOptional Diagnostic.emit OptionalHookEffect.CasebookObservation (fun () ->
                        collectCasebookObservation toolInput toolOutput)
                    |> ignore

            // tool.execute.after 的具名 stage 序列。次序即合同。
            let toolAfter (toolInput: obj) (toolOutput: obj) =
                task {
                    afterStageRestoreProtocolArguments toolInput toolOutput
                    do! afterStageRequirementGrounding toolInput toolOutput
                    afterStageCasebookObservation toolInput toolOutput
                }

            let chatMessage =
                registeredHook HookKey.ChatMessage (curriedHook wired.ChatMessageHook)

            let chatParamsRegistration =
                registeredHook HookKey.ChatParams (curriedHook chatParams)

            let messagesTransform =
                registeredHook HookKey.MessagesTransform (curriedHook (box ownedTransform))

            let systemTransformRegistration =
                registeredHook HookKey.SystemTransform (pairedHook (box systemTransform))

            let syncHostLanguagePreference (config: obj) =
                let lang = config?language

                if not (isNull lang) then
                    ProviderLanguageBinding.setHostConfigPreference (string lang)
                    ProviderLanguageBinding.refreshGlobalLanguage ()

            let configurePluginHost (config: obj) =
                if not (isNull config) then
                    config?snapshot <- box false
                    syncHostLanguagePreference config
                    ManagerConfig.configureManager config |> ignore
                    scope.RecordCompactionSettingGap(HostCompactionGate.enforceSettings config)

            let config = registeredHook HookKey.Config (unaryHook (box configurePluginHost))

            let sessionCompacting =
                registeredHook HookKey.SessionCompacting (pairedHook (box HostCompactionGate.onSessionCompacting))

            let compactionAutoContinue =
                registeredHook
                    HookKey.CompactionAutoContinue
                    (pairedHook (box HostCompactionGate.onCompactionAutoContinue))

            let toolDefinitionRegistration =
                registeredHook HookKey.ToolDefinition (pairedHook (box toolDefinition))

            let toolBeforeRegistration =
                registeredHook HookKey.ToolBefore (pairedHook (box toolBefore))

            let toolAfterRegistration =
                registeredHook HookKey.ToolAfter (pairedHook (box toolAfter))

            /// context-compression [001]: the provider's own billing for the
            /// session's latest assistant turn, read off the host's message
            /// events. It is a datum about the model's own state, never a
            /// capacity derivation on our side.
            let tokenUse (raw: obj) : (int * int) option =
                let info = if isNull raw?properties then null else raw?properties?info
                let tokens = if isNull info then null else info?tokens
                let cached = if isNull tokens then null else tokens?cache?read
                let uncached = if isNull tokens then null else tokens?input

                if
                    emitJsExpr
                        (cached, uncached)
                        "typeof $0 === 'number' && typeof $1 === 'number' && $0 >= 0 && $1 >= 0"
                then
                    Some(int (unbox<float> cached), int (unbox<float> uncached))
                else
                    None

            let recordProviderTokenUse (rawInput: obj) =
                let raw = HostEventEnvelope.unwrap rawInput

                let observation =
                    if HostEventEnvelope.eventTypeOf raw = "message.updated" then
                        tokenUse raw
                        |> Option.bind (fun usage ->
                            HostEventEnvelope.tryMessageSessionId rawInput
                            |> Option.map (fun sessionId -> sessionId, usage))
                    else
                        None

                observation
                |> Option.iter (fun (sessionId, (cached, uncached)) ->
                    ProviderTokenUse.record (SessionId.value sessionId) cached uncached)

            let observeEvent raw =
                task {
                    recordProviderTokenUse raw
                    do! settleViewCheckpointEvent raw
                    do! settleUserQuestion raw
                    do! settlePostS3Deliver raw
                    do! wired.ObserveEvent raw
                }

            let event = registeredHook HookKey.Event (unaryHook (box observeEvent))

            let dispose =
                let disposeAll () = scope.DisposeAsync()

                registeredHook HookKey.Dispose (nullaryHook (box disposeAll))

            let hooks =
                createObj (
                    [ chatMessage
                      chatParamsRegistration
                      messagesTransform
                      systemTransformRegistration
                      config
                      sessionCompacting
                      compactionAutoContinue
                      toolDefinitionRegistration
                      toolBeforeRegistration
                      toolAfterRegistration
                      event
                      dispose ]
                    @ (toolRegistration
                       |> Option.map (fun registration -> [ "tool", registration.Tools ])
                       |> Option.defaultValue [])
                )

            return box hooks
        }
