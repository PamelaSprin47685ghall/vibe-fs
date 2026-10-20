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
open Wanxiangshu.Context.Trace
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
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Investigation.WarmStart

open Wanxiangshu.Host
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
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

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
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

open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.OpenCode.Host
open PluginHostInterop

module PluginHooks =

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasOwnProperty (target: obj) (prop: string) : bool = jsNative

    [<Emit("$0 === undefined")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// The top-level context-compression declaration of one js-* call, read
    /// from the flat arguments `prefixUntil` and `thenMap`. The retired
    /// `view` wrapper is refused by the call surface, so it is never read
    /// here. A call carries a declaration iff it gives either key; the
    /// declaration object is assembled exactly as the call surface
    /// assembles it, so the same validator decides.
    let private declarationOf (input: obj) : obj option =
        let fields =
            if isNull input then
                None
            else
                let prefixUntil = input?prefixUntil
                let thenMap = input?thenMap

                Some(prefixUntil, thenMap)

        fields
        |> Option.filter (fun (prefixUntil, thenMap) ->
            not (isUndefined prefixUntil && isUndefined thenMap))
        |> Option.map (fun (prefixUntil, thenMap) ->
            createObj [ "prefixUntil" ==> prefixUntil; "thenMap" ==> thenMap ])

    /// A declaration counts only when it is valid: an invalid one never
    /// committed a view, so it must never commit a checkpoint.
    ///
    /// The call surface refuses a call that carries more than one of
    /// program / unpark / abandon; such a call never ran, so it commits no
    /// checkpoint either. That judgement is the call surface's own law, so
    /// it is asked, not recounted here.
    let private declaredViewOf (input: obj) : bool =
        let conflictingState =
            Wanxiangshu.Repository.Programming.Js.OpenCode.JsToolWorkflow.conflictingInputReason input
            |> Option.isSome

        if conflictingState then
            false
        else
            declarationOf input
            |> Option.map (fun declaration ->
                Wanxiangshu.Repository.Programming.Js.Anchor.AnchorSurface.parse declaration
                |> Result.isOk)
            |> Option.defaultValue false

    /// obligation-ledger-005 / context-compression-028: the view checkpoint
    /// admission law (one append per exact completed call, in-flight duplicates
    /// join, final failures never rewritten, WriterUnavailable released) is owned
    /// by wanxiangli/compression; this host keeps the journal write and the
    /// waiter rendezvous on the one in-flight Task.
    [<AllowNullLiteral>]
    type private CheckpointAdmission =
        abstract observe: terminal: obj -> string
        abstract settle: call: obj * outcome: obj -> string

    [<AllowNullLiteral>]
    type private CompressionCheckpoints =
        abstract checkpoints: unit -> CheckpointAdmission

    [<Import("Compression", "wanxiangli/compression")>]
    let private createCompression: unit -> CompressionCheckpoints = jsNative

    let private checkpointCall (sessionText: string) (callId: ToolCallId) =
        createObj [ "session" ==> sessionText; "callId" ==> ToolCallId.value callId ]

    let private appendOutcome (failure: JournalAppendFailure) =
        match failure with
        | JournalAppendFailure.WriterUnavailable _ -> "WriterUnavailable"
        | JournalAppendFailure.WriteUnknown _ -> "WriteUnknown"
        | JournalAppendFailure.NoNewWriteReleaseFailed _ -> "NoNewWriteReleaseFailed"
        | JournalAppendFailure.FactRejected _ -> "FactRejected"

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

            let planStageFor (sessionId: SessionId) : Task<string option> =
                task {
                    match roleFor sessionId, workspaceDirectory with
                    | Some Role.Plan, Some workspace ->
                        let! commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot workspace

                        return
                            Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected
                                commonDir
                                (SessionId.value sessionId)
                            |> Option.bind (fun work -> stagePathFor work.Stage work.ActiveId)
                    | _ -> return None
                }

            let systemTransform = ProviderSystemTransform.createWith roleFor planStageFor


            // CASE-003: typed capture at the tool boundary — shared
            // CasebookLifecycle.collector; marker flag gates the after-hook.
            // Store IO stays out of SpikePlugin (unified-store dual-write gate).
            let casebookEnabled =
                match workspaceDirectory with
                | Some ws -> CasebookFeature.isEnabled ws
                | None -> false

            // Programming-surface calls carrying a non-empty `view` argument establish
            // the compression checkpoint: one durable checkpoint per successfully
            // completed call, keyed by exact call id (admission: wanxiangli/compression).
            let viewCheckpointAdmission = createCompression().checkpoints ()
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

                    // A participant's return is the this.deliver view.
                    let deliveredView =
                        Some(fun sessionId ->
                            Task.FromResult(
                                Wanxiangshu.Repository.Programming.Js.JsDeliveredValues.tryPeekRendered sessionId
                            ))

                    let childViewForRun = fun _ _ -> Task.FromResult None

                    let! casebookToolSpecs =
                        match workspaceDirectory with
                        | Some ws ->
                            CasebookTools.buildSpecs (ToolHostCodec.factory toolModule) ws boot.CasebookSettlements
                        | None -> Task.FromResult []

                    let! toolRegistration =
                        toolHooks
                            toolModule
                            sessionPort
                            host.CausalWaitObserver
                            host.RootWorkspace
                            journal
                            workspaceDirectory
                            scope
                            wired.CurrentPhysicalUserMessage
                            onRunStarted
                            deliveredView
                            childViewForRun
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

            let toolDefinition (_toolInput: obj) (_toolOutput: obj) = ()

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

                let declaredView = declaredViewOf input

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
                    && declaredView
                    ->
                    Some(SessionId.value sessionId, ToolCallId.create callId, status)
                | _ -> None

            /// 条款 6：中断未确认是呈现面的失败，不是事实面的失败。提问已经
            /// 提交（durable pending 仍在等待事实），宿主也已接受工具结果；
            /// 中断失败只记诊断——不崩、不抹掉已提交的事实，也不把已成立的
            /// 交付呈现为失败。
            let reportUserQuestionInterruptUnconfirmed (sessionId: string) (reason: string) =
                Diagnostic.emit
                    "ask-interrupt-not-confirmed"
                    [ "session_id", sessionId
                      "provider_error", reason
                      "result", "interrupt-not-confirmed" ]

            /// 条款 6：缺精确 provider run 身份时没有 attempt 围栏，就不该
            /// 中断（不得误停新输入或后任），只记诊断，不崩。
            let reportUserQuestionMissingProviderRun (sessionId: string) =
                Diagnostic.emit "ask-observation-missing-provider-run" [ "session_id", sessionId; "result", "unbound" ]

            let settleUserQuestion rawInput =
                task {
                    // `this.ask` inside a plan program records the same durable
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

                        match result with
                        | Ok() -> ()
                        | Error reason -> reportUserQuestionInterruptUnconfirmed sessionId reason
                    | Some(sessionId, _, "completed", None), Some registration when
                        registration.Runtime.IsWaitingForUser sessionId
                        ->
                        reportUserQuestionMissingProviderRun sessionId
                    | _ -> ()
                }

            /// 条款 4/6：事件钩子只做观测登记——宿主已接受本次调用 + 精确
            /// attempt 的交付工具结果（呈现面）。提交面事实由交付发生的那一次
            /// 登记写入（它携带交付当时的物理输入），事件钩子不再新造物理围栏，
            /// 新输入因此不会被旧交付停止。物理中断由 transform 钩子的交付
            /// 裁决执行；交付已提交不因中断未确认而呈现为失败。
            let settlePostS3Deliver rawInput =
                task {
                    match toolTerminalObservation "deliver" rawInput, toolRegistration with
                    | Some(sessionId, callId, "completed", Some providerRun), Some registration ->
                        registration.Runtime.RecordDeliveryPresented(SessionId.create sessionId, providerRun, callId)
                    | Some(sessionId, _, "completed", None), Some registration when
                        registration.Runtime.HasEstablishedDelivery sessionId
                        ->
                        Diagnostic.emit
                            "post-s3-deliver-observation-missing-provider-run"
                            [ "session_id", sessionId; "result", "unbound" ]
                    | _ -> ()
                }

            let requiredViewJournal () =
                journal
                |> Option.defaultWith (fun () -> invalidOp "view compression checkpoint requires a durable journal")

            let appendTerminalViewCheckpoint key sessionText callId =
                task {
                    let call = checkpointCall sessionText callId

                    try
                        do! appendViewCheckpoint (requiredViewJournal ()) sessionText callId
                        viewCheckpointAdmission.settle (call, "committed") |> ignore
                        pendingViewCheckpointAppends.Remove(key) |> ignore
                    with :? JournalAppendException as appendFailure ->
                        match viewCheckpointAdmission.settle (call, appendOutcome appendFailure.Failure) with
                        | "released" -> pendingViewCheckpointAppends.Remove(key) |> ignore
                        | _ -> ()

                        return raise appendFailure
                }

            /// One durable checkpoint per exact terminal call. A repeated Host
            /// part update for the same call id is a replay, not a second fact.
            /// obligation-ledger-005: concurrent duplicates of the same terminal
            /// share the one in-flight append and observe its final outcome; a
            /// failed append propagates its error to every waiter. Which terminal
            /// appends, joins or is skipped, and which outcomes release the call
            /// for a real retry, is answered by the compression admission ledger.
            let settleViewCheckpointTerminal (sessionText, callId, status) =
                let key = SessionId.create sessionText, callId

                let terminal =
                    createObj
                        [ "session" ==> sessionText
                          "callId" ==> ToolCallId.value callId
                          "status" ==> status ]

                match viewCheckpointAdmission.observe terminal with
                | "append" ->
                    let append = appendTerminalViewCheckpoint key sessionText callId
                    pendingViewCheckpointAppends[key] <- append
                    append
                | "join" -> pendingViewCheckpointAppends[key]
                | _ -> Task.FromResult(())

            let settleViewCheckpointEvent rawInput =
                viewTerminalObservation rawInput
                |> Option.map settleViewCheckpointTerminal
                |> Option.defaultValue (Task.FromResult(()))

            let checkManagerReviewPermissions toolName toolInput =
                task {
                    let sessionId = toolField toolInput "sessionID"

                    let blockedNative =
                        toolName = "todowrite-for-engineer" || toolName = "todowrite-for-devops"

                    if roleFor (SessionId.create sessionId) = Some Role.Manager && blockedNative then
                        invalidOp (sprintf "Manager tool '%s' is refused on the review gate" toolName)
                }

            let hideReviewArguments owner toolName args =
                if ManagerReviewTools.isReviewTool toolName then
                    ManagerReviewContract.hideForCall owner args

            let hideProtocolArguments owner toolName args =
                try
                    hideReviewArguments owner toolName args
                with error ->
                    ManagerReviewContract.restore args
                    raise error

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

            let hiddenArgumentState owner toolName (toolOutput: obj) =
                let isReview = ManagerReviewTools.isReviewTool toolName

                if not isReview || isNull toolOutput || isNull toolOutput?args then
                    HiddenProtocolArguments.NotHidden
                else
                    hiddenReviewState owner isReview toolOutput?args

            let protocolArgumentsAreHidden owner toolName toolOutput =
                match hiddenArgumentState owner toolName toolOutput with
                | HiddenProtocolArguments.SameCall -> true
                | HiddenProtocolArguments.DifferentCallOrChangedArguments ->
                    invalidOp
                        "Invalid investigation estimate arguments: hidden arguments belong to another call or were changed before restoration"
                | HiddenProtocolArguments.NotHidden -> false

            // 隐藏前再核一次：本调用尚未隐藏且 args 可读才执行 hide。
            let hideProtocolArgumentsIfShown owner toolName (toolOutput: obj) =
                if
                    not (protocolArgumentsAreHidden owner toolName toolOutput)
                    && not (isNull toolOutput)
                    && not (isNull toolOutput?args)
                then
                    hideProtocolArguments owner toolName toolOutput?args

            let prepareNewProtocolArguments owner toolName toolInput toolOutput =
                task {
                    do! observeDelegatedToolEstimate toolInput
                    hideProtocolArgumentsIfShown owner toolName toolOutput
                }

            let beforeStagePrepareProtocolArguments toolName toolInput toolOutput =
                task {
                    let owner = argumentCallOwner toolName toolInput

                    if not (protocolArgumentsAreHidden owner toolName toolOutput) then
                        do! prepareNewProtocolArguments owner toolName toolInput toolOutput
                }

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
                    | Some registration when
                        registration.Runtime.IsWaitingForUser sessionId
                        && not (
                            // LC-017: 同一围栏的 ask/deliver 精确重放可穿越等待门。
                            // 其余工具在等待期间仍拒绝。
                            let isReplayExemptTool =
                                toolName = "deliver"
                                || toolName.StartsWith("todowrite-for-", StringComparison.OrdinalIgnoreCase)

                            let replayFence =
                                ToolHostCodec.decodeContext toolInput
                                |> fun ctx ->
                                    match ctx.ProviderRunId, ctx.ToolCallId with
                                    | Some run, Some call -> Some(run, call)
                                    | _ -> None

                            isReplayExemptTool
                            && replayFence = registration.Runtime.WaitingQuestionFenceFor sessionId
                        )
                        ->
                        invalidOp (
                            ProviderProse.render
                                (ProviderLanguageBinding.forSessionText sessionId)
                                "tool/ask-waiting"
                                Map.empty
                        )
                    | _ -> ()

                    do! checkManagerReviewPermissions toolName toolInput

                    do! beforeStagePrepareProtocolArguments toolName toolInput toolOutput
                }

            let restoreArgumentsForTarget owner (target: obj) =
                if not (isNull target) && not (isNull target?args) then
                    ManagerReviewContract.restoreForCall owner target?args

            let afterStageRestoreProtocolArguments (toolInput: obj) (toolOutput: obj) =
                let toolName = toolField toolInput "tool"
                let owner = argumentCallOwner toolName toolInput

                restoreArgumentsForTarget owner toolInput
                restoreArgumentsForTarget owner toolOutput

            // after stage 2: 可选 casebook 观察。失败只发诊断，不改关键结果
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

            let syncHostToolLimits (config: obj) =
                let toolOutput = config?tool_output

                if not (isNull toolOutput) then
                    Wanxiangshu.Host.Contract.ToolResultBound.setHostLimits toolOutput?max_lines toolOutput?max_bytes

            let configurePluginHost (config: obj) =
                if not (isNull config) then
                    config?snapshot <- box false
                    syncHostLanguagePreference config
                    ManagerConfig.configureManager config |> ignore
                    scope.RecordCompactionSettingGap(HostCompactionGate.enforceSettings config)
                    syncHostToolLimits config

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
