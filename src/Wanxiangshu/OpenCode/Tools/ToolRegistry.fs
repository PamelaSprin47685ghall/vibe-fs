namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host
open Wanxiangshu.Context.Scope
open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Participant.ManagedAgents

open Wanxiangshu.Foundation.TerminalSignal

open System
open FsToolkit.ErrorHandling
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation.Advisor
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Tool
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Planning
open Wanxiangshu.Mission.Work.OpenCode
open Wanxiangshu.Mission.Work
open Wanxiangshu.Execution.Routing
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Process

/// Assembly-only registry: tool behavior lives in one vertical verb module;
/// per-session resources live in ToolRuntimeScope.
type ToolRegistration =
    { Tools: obj
      Runtime: ToolRuntimeScope }

module ToolRegistry =

    [<RequireQualifiedAccess>]
    module Path =
        /// Algorithm G1: deliver is blocked while any parked site is unresolved.
        [<Literal>]
        let DeliverBlockedByParked = "tool/js-program/parked-deliver-blocked"

    let private lang (ctx: HostToolContext) =
        let sessionText = ctx.SessionId

        ProviderLanguageBinding.forSessionText sessionText

    [<Emit("typeof $0 === 'string'")>]
    let private isStringValue (value: obj) : bool = jsNative

    [<Emit("$0 === null || $0 === undefined")>]
    let private isNullish (value: obj) : bool = jsNative

    [<Emit("(() => { const out = {}; for (const k of Object.keys($0)) out[k] = $0[k]; out[$1] = $2; return out; })()")>]
    let private withField (source: obj) (key: string) (value: obj) : obj = jsNative

    /// A delegate payload is built inside the JS sandbox realm, so its
    /// prototype is that realm's `Object.prototype` — never the host's. The
    /// host's plain-record check compares realms, so the payload is copied
    /// here into a host-realm plain record: own enumerable data properties
    /// only (accessors and non-enumerable properties are skipped). The copy
    /// is a snapshot; the program may still mutate its own object afterwards.
    [<Emit("(() => { const out = {}; const src = $0; if (src === null || src === undefined || typeof src !== 'object') return out; for (const key of Object.keys(src)) { const descriptor = Object.getOwnPropertyDescriptor(src, key); if (descriptor && Object.prototype.hasOwnProperty.call(descriptor, 'value')) { out[key] = descriptor.value; } } return out; })()")>]
    let private plainRecordCopy (source: obj) : obj = jsNative

    let private devopsIntentText (payload: obj) : string =
        if isStringValue payload then
            unbox<string> payload
        elif isNullish payload then
            ""
        else
            let prop = payload?intent
            if isNullish prop then "" else string prop

    let private advisorIntentText (intent: obj) : string =
        if isStringValue intent then unbox<string> intent else ""

    let private advisorRefusal (code: string) (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

    let private advisorSeatAnswer (runtime: ToolRuntimeScope) (ctx: HostToolContext) (intent: obj) : Task<obj> =
        task {
            let sessionId = ctx.SessionId
            let intentText = advisorIntentText intent

            let deps: AdvisorSeatDeps =
                { Sessions = runtime.Sessions
                  Snapshot = runtime.Snapshot
                  Journal = runtime.Journal
                  Directory = runtime.WorkspaceDirectory
                  Participant = ManagedAgent.nameOf Role.Advisor
                  BindDelegation = Surface.bindDelegation }

            let! answered = AdvisorSeat.consult deps sessionId intentText

            match answered with
            | Ok advice -> return createObj [ "ok" ==> true; "advice" ==> advice ]
            | Error reason -> return advisorRefusal "advisor-unavailable" reason
        }

    let private advisorConsult (runtime: ToolRuntimeScope) (ctx: HostToolContext) (intent: obj) : Task<obj> =
        advisorSeatAnswer runtime ctx intent

    let private reviewRefusal (code: string) (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

    let private executePlanReviewWithIncumbency
        (gitCommonDir: string)
        (ctx: HostToolContext)
        (incumbencyId: string)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (findings: obj)
        : Task<obj> =
        let requestId =
            ProviderRunIdentity.value providerRun + ":" + ToolCallId.value toolCallId

        PlanningSurface.executeReview gitCommonDir ctx.SessionId incumbencyId requestId findings null

    let private executePlanReviewWithContext
        (runtime: ToolRuntimeScope)
        (ctx: HostToolContext)
        (directory: string)
        (providerRun: ProviderRunIdentity)
        (toolCallId: ToolCallId)
        (findings: obj)
        : Task<obj> =
        task {
            let! gitCommonDir = RuntimePath.stateRoot directory
            let! work = runtime.LifecycleWorkFor ctx.SessionId
            let activeIdOpt = work |> Option.bind (fun inspected -> inspected.ActiveId)

            match activeIdOpt with
            | Some incumbencyId ->
                return! executePlanReviewWithIncumbency gitCommonDir ctx incumbencyId providerRun toolCallId findings
            | None -> return reviewRefusal "review-unavailable" "no active incumbency for this plan work"
        }

    let private executePlanReview (runtime: ToolRuntimeScope) (ctx: HostToolContext) (findings: obj) : Task<obj> =
        match runtime.WorkspaceDirectory, ctx.ProviderRunId, ctx.ToolCallId with
        | Some directory, Some providerRun, Some toolCallId ->
            executePlanReviewWithContext runtime ctx directory providerRun toolCallId findings
        | _ ->
            Task.FromResult(reviewRefusal "review-unavailable" "review requires an owning provider call and workspace")

    /// The `this.review(findings)` member host closure. Plan dispatches to the
    /// plan review backend (already data-shaped); every other admitted caller
    /// dispatches to the Manager backend. The request identity is the exact
    /// provider-run / tool-call fence, so distinct calls are distinct facts
    /// and an exact retry replays.
    let private reviewMember (runtime: ToolRuntimeScope) (ctx: HostToolContext) (findings: obj) : Task<obj> =
        if runtime.IsRole(ctx, Role.Plan) then
            executePlanReview runtime ctx findings
        else
            ReviewBackendManager.executeMember runtime ctx findings

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
        (waitObserver: IWaitObserver)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (currentPhysicalUserMessage: string -> string option)
        (sessionDirectories: Dictionary<string, string>)
        (onRunStarted: (SessionId -> Role -> string option -> unit) option)
        (viewForSession: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (beginToolExecution: string -> unit)
        (endToolExecution: string -> unit)
        (eventPort: IEventObservationPort option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (casebookToolSpecs: ToolSpec list)

        (jsTransactionPersistence: IJsTransactionPersistence option)
        (jsLifecyclePersistence: IJsLifecyclePersistence option)
        (continueManagerLoop: SessionId -> string -> Task<Result<unit, string>>)
        (childViewForRun: (SessionId -> ProviderRunIdentity -> Task<string option>) option)
        (userNotify: (string -> string -> unit) option)
        =
        let factory = ToolHostCodec.factory toolModule
        let providerLanguage = ProviderLanguageBinding.readGlobalPreference ()

        let jsProse: JsCanonicalDescription.Prose =
            JsDescriptionAssets.load providerLanguage

        /// Where the programming surface of one session executes. A Manager, child
        /// or DevOps session is created rooted at the directory its owner
        /// registered (a Change road's worktree), so the surface follows the
        /// session instead of the plugin's project directory.
        let jsRootOf (sessionId: string) =
            JsToolPolicy.rootForSession sessionDirectories (defaultArg workspaceDirectory "") sessionId

        let runtime =
            new ToolRuntimeScope(
                sessionPort,
                waitObserver,
                rootWorkspace,
                journal,
                workspaceDirectory,
                sessionParents,
                currentPhysicalUserMessage,
                sessionDirectories,
                onRunStarted,
                viewForSession,
                snapshot,
                cancelSignals,
                ?childViewForRun = childViewForRun,
                continueManagerLoop = continueManagerLoop,
                ?eventPort = eventPort
            )

        // work-028: the real delegation capability — the SyncDelegate
        // runtime runs the bound fixed DevOps for this work synchronously and
        // hands back its complete delivered view. A missing runtime fails closed.
        let devopsDelegateOf (syncRuntime: SyncDelegateRuntime option) (ctx: HostToolContext) : string -> Task<string> =
            let invokeRuntime (runtime: SyncDelegateRuntime) (intent: string) : Task<string> =
                task {
                    let! outcome = runtime.Invoke(ctx.SessionId, SyncDelegateRole.DevOps, intent)

                    return
                        match outcome with
                        | Ok record -> record
                        | Error reason ->
                            raise (InvalidOperationException(sprintf "DevOps delegation failed: %s" reason))
                }

            fun (intent: string) ->
                task {
                    match syncRuntime with
                    | None -> return raise (InvalidOperationException "sync delegate runtime unavailable")
                    | Some runtime -> return! invokeRuntime runtime intent
                }

        let mutable cachedGitCommonDir: string option = None

        let populateStateRoot (ws: string) : Task<unit> =
            task {
                let! dir = RuntimePath.stateRoot ws
                cachedGitCommonDir <- Some dir
            }

        let ensureStateRoot (workspace: string option) : Task<unit> =
            match cachedGitCommonDir, workspace with
            | None, Some ws -> populateStateRoot ws
            | _ -> Task.FromResult()

        let askGitCommonDir (directory: string option) : Task<string> =
            task {
                match directory with
                | Some workspace ->
                    let! commonDir = Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot workspace
                    return commonDir
                | None -> return ""
            }

        /// The assistant message that owns this call: the provider run IS that
        /// message id. The call's prose and the physical message it answers
        /// are both read from this one message.
        let owningAssistantMessage
            (messages: SessionMessage list)
            (providerRun: ProviderRunIdentity)
            : SessionMessage option =
            let providerRunId = ProviderRunIdentity.value providerRun

            messages
            |> List.tryFind (fun message -> message.Id = providerRunId && message.Role = "assistant")

        /// The formal prose of the assistant message that owns the call: the
        /// text parts of that one message, joined by newlines. A call whose
        /// assistant message carries no prose has no formal text, and a
        /// witness without prose is no witness.
        let formalTextOf (message: SessionMessage) : string option =
            let text =
                message.Parts
                |> Array.choose (function
                    | MessagePart.Text text -> Some text
                    | _ -> None)
                |> String.concat "\n"

            if String.IsNullOrWhiteSpace text then None else Some text

        /// The physical user message this call answers.
        ///
        /// The parent of the assistant message is the run's physical input
        /// only when that parent is a user message: inside one run the next
        /// assistant message hangs under the tool result, not under the user.
        /// Otherwise the nearest user message before this call answers: a
        /// delivery always belongs to the turn that asked for it, and relying
        /// on the routing's current physical alone loses that turn as soon as
        /// the lease moves. The routing value is the last fallback, not the
        /// first source.
        let physicalUserMessageOf
            (messages: SessionMessage list)
            (message: SessionMessage)
            (currentPhysical: string option)
            : PhysicalUserMessageId option =
            let parentIsUserMessage =
                match message.ParentId with
                | Some parent when not (String.IsNullOrWhiteSpace parent) ->
                    messages
                    |> List.exists (fun candidate -> candidate.Id = parent && candidate.Role = "user")
                | _ -> false

            if parentIsUserMessage then
                message.ParentId |> Option.map PhysicalUserMessageId.create
            else
                let nearestUser =
                    messages
                    |> List.takeWhile (fun candidate -> candidate.Id <> message.Id)
                    |> List.rev
                    |> List.tryPick (fun candidate -> if candidate.Role = "user" then Some candidate.Id else None)

                (nearestUser |> Option.orElse currentPhysical)
                |> Option.map PhysicalUserMessageId.create

        /// The task a delivery serves.
        ///
        /// A lifecycle work names the tenure the delivery closes: the
        /// retirement that carries this very call's fence is the durable fact
        /// of that tenure, so the tenure and the retirement id come from one
        /// fact. A delegated child names its own handle — the agent handle the
        /// delegation bound to that child session. Everything else is this very
        /// invocation, named by the tool call that carries it.
        let deliveryTaskOf
            (durable: AgentJournal)
            (sessionId: SessionId)
            (providerRun: ProviderRunIdentity)
            (toolCallId: ToolCallId)
            (work: Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option)
            : DeliveryTaskReference * string option =
            let providerRunId = ProviderRunIdentity.value providerRun
            let toolCallIdText = ToolCallId.value toolCallId

            let lifecycleTask =
                work
                |> Option.bind (fun work ->
                    work.Retirements
                    |> List.tryFind (fun retirement ->
                        retirement.Fence.ProviderRunId = providerRunId
                        && retirement.Fence.ToolCallId = toolCallIdText)
                    |> Option.map (fun retirement ->
                        DeliveryTaskReference.Lifecycle(SessionId.value sessionId, retirement.IncumbencyId),
                        Some retirement.RetirementId))

            let childHandle =
                (AgentJournal.snapshot durable).AgentProjections.HandleByChildSession
                |> Map.tryFind sessionId
                |> Option.bind (fun record ->
                    let agentHandle =
                        HandleId.tryAgent record.Handle
                        |> Option.map AgentHandleId.value
                        |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))

                    agentHandle
                    |> Option.orElse (
                        Some record.TargetAgent
                        |> Option.filter (fun target -> not (String.IsNullOrWhiteSpace target))
                    ))

            match lifecycleTask, childHandle with
            | Some task, _ -> task
            | None, Some handle -> DeliveryTaskReference.ChildWork handle, None
            | None, None -> DeliveryTaskReference.Invocation toolCallIdText, None

        let witnessSettlement
            (durable: AgentJournal)
            (sessionId: SessionId)
            (providerRun: ProviderRunIdentity)
            (toolCallId: ToolCallId)
            logicalRunId
            authorityRoot
            physical
            (formalText: string)
            work
            : DeliverySettlement =
            let deliveryTask, retirementId =
                deliveryTaskOf durable sessionId providerRun toolCallId work

            DurableDelivery(
                { LogicalRunId = logicalRunId
                  AuthorityRoot = authorityRoot
                  PhysicalUserMessageId = physical
                  Task = deliveryTask
                  RetirementId = retirementId
                  FormalText = formalText },
                DeliveryWitnessWriter.create durable (fun witness ->
                    // The durable witness is committed by now: the
                    // registration only records the committed attempt, so the
                    // transform hook's delivery adjudication can stop it.
                    DeliveryAttemptLedger.recordEstablished
                        { SessionId = witness.SessionId
                          ProviderRun = witness.ProviderRun
                          CallId = witness.ToolCallId }
                        (Some witness.PhysicalUserMessageId))
            )

        /// The formal prose and the physical input of the assistant message
        /// that made the call; either missing leaves the call unattributed.
        ///
        /// The prose may be the immediately preceding assistant text of the
        /// same turn: the delivery contract tells the model to state the
        /// result in prose and then call `deliver`, and providers commonly
        /// split those into two assistant messages. Only text after this
        /// call's physical input is accepted, so an older turn's prose can
        /// never attribute a new delivery.
        let formalTextOfTurn (messages: SessionMessage list) (owner: SessionMessage) (physical: PhysicalUserMessageId) =
            let beforeOwner = messages |> List.takeWhile (fun message -> message.Id <> owner.Id)

            let boundary =
                beforeOwner
                |> List.tryFindIndex (fun message -> message.Id = PhysicalUserMessageId.value physical)

            match boundary with
            | None -> None
            | Some index ->
                beforeOwner
                |> List.skip (index + 1)
                |> List.rev
                |> List.tryFind (fun candidate -> candidate.Role = "assistant")
                |> Option.bind formalTextOf

        let messageAttribution
            (ctx: HostToolContext)
            (providerRun: ProviderRunIdentity)
            (messages: SessionMessage list)
            : (string * PhysicalUserMessageId) option =
            owningAssistantMessage messages providerRun
            |> Option.bind (fun message ->
                physicalUserMessageOf messages message (currentPhysicalUserMessage ctx.SessionId)
                |> Option.map (fun physical ->
                    let formalText =
                        formalTextOf message
                        |> Option.orElseWith (fun () -> formalTextOfTurn messages message physical)
                        |> Option.defaultValue ""

                    formalText, physical))

        let attributedSettlement
            (ctx: HostToolContext)
            (durable: AgentJournal)
            (sessionId: SessionId)
            (providerRun: ProviderRunIdentity)
            (toolCallId: ToolCallId)
            logicalRunId
            authorityRoot
            (messages: SessionMessage list)
            : Task<DeliverySettlement option> =
            match messageAttribution ctx providerRun messages with
            | None -> Task.FromResult None
            | Some(formalText, physical) ->
                task {
                    // The lifecycle work is read only for a
                    // call that already has its attribution:
                    // the work journal is a durable read and
                    // must not be paid for calls that never
                    // get a witness.
                    let! work = runtime.LifecycleWorkFor ctx.SessionId

                    return
                        Some(
                            witnessSettlement
                                durable
                                sessionId
                                providerRun
                                toolCallId
                                logicalRunId
                                authorityRoot
                                physical
                                formalText
                                work
                        )
                }

        let loadedSettlement
            (ctx: HostToolContext)
            (durable: AgentJournal)
            (sessionId: SessionId)
            (providerRun: ProviderRunIdentity)
            (toolCallId: ToolCallId)
            (loadMessages: unit -> Task<Result<SessionMessage list, 'error>>)
            logicalRunId
            authorityRoot
            : Task<DeliverySettlement option> =
            task {
                let! loaded = loadMessages ()

                match loaded with
                | Error err -> return Some(DeliverySettlement.ContextUnavailable(sprintf "%A" err))
                | Ok messages ->
                    return!
                        attributedSettlement
                            ctx
                            durable
                            sessionId
                            providerRun
                            toolCallId
                            logicalRunId
                            authorityRoot
                            messages
            }

        /// The delivery settlement of one js-* call: the host's attribution of
        /// the call and the durable place its witness goes.
        ///
        /// Every field is read from the source the read side reads. A durable
        /// host whose call cannot be attributed refuses the delivery: no
        /// witness is invented, and a process-live cell alone must never
        /// present a return the caller's join cannot receive. Only a host
        /// without a journal is the harness case and keeps the cell-only
        /// behaviour.
        let unwrapAttributed (attributed: DeliverySettlement option) : DeliverySettlement option =
            match attributed with
            | Some settlement -> Some settlement
            | None ->
                Some(
                    UnattributedDelivery
                        "the delivering assistant message carries no prose or physical input to own the witness"
                )

        let settlementFromSnapshot
            (ctx: HostToolContext)
            (durable: AgentJournal)
            (snapshotPort: ISessionSnapshotPort)
            (providerRun: ProviderRunIdentity)
            (toolCallId: ToolCallId)
            : Task<DeliverySettlement option> =
            let sessionId = SessionId.create ctx.SessionId

            Wanxiangshu.Interaction.Authority.PromptAuthorityProjectionQueries.activeProfile
                sessionId
                (AgentJournal.snapshot durable).AgentProjections
            |> Option.map (fun profile -> profile.LogicalRunId, profile.AuthorityRootUserMessageId)
            |> Option.filter (fun (logicalRunId, authorityRoot) ->
                not (
                    String.IsNullOrWhiteSpace(LogicalRunId.value logicalRunId)
                    || String.IsNullOrWhiteSpace(AuthorityRootUserMessageId.value authorityRoot)
                ))
            |> Option.map (fun (logicalRunId, authorityRoot) ->
                task {
                    let! attributed =
                        loadedSettlement
                            ctx
                            durable
                            sessionId
                            providerRun
                            toolCallId
                            (fun () -> snapshotPort.GetMessages sessionId)
                            logicalRunId
                            authorityRoot

                    return unwrapAttributed attributed
                })
            |> Option.defaultValue (
                Task.FromResult(
                    Some(UnattributedDelivery "the delivery call has no active logical run or authority root")
                )
            )

        let settlementOfDurable (ctx: HostToolContext) (durable: AgentJournal) : Task<DeliverySettlement option> =
            match runtime.Snapshot, ctx.ProviderRunId, ctx.ToolCallId with
            | Some snapshotPort, Some providerRun, Some toolCallId ->
                settlementFromSnapshot ctx durable snapshotPort providerRun toolCallId
            | _ ->
                Task.FromResult(
                    Some(
                        UnattributedDelivery
                            "the delivery call has no provider run, tool call or host snapshot to be attributed"
                    )
                )

        let settlementOf (ctx: HostToolContext) : Task<DeliverySettlement option> =
            match journal with
            | None -> Task.FromResult None
            | Some durable -> settlementOfDurable ctx durable

        let generatedJsSpecFor (role: Role) =
            JsToolGenerator.generate (string role) (OfficeCapability.permissions role) jsProse
            |> Option.map (fun surface ->
                let reviewAllowed (ctx: HostToolContext) : Task<bool> =
                    task {
                        let! facts = runtime.ManagerCapabilityFactsFor ctx.SessionId
                        let! frozen = runtime.IsRetirementFrozen ctx.SessionId

                        return
                            facts.HasActiveIncumbency
                            && not facts.HasAssessment
                            && facts.CleanupBlockerDigest.IsNone
                            && not frozen
                    }

                // reviewFinishedOnly: a Plan tenure's draft writes are locked
                // until its first review is finished. Ordinary reads stay
                // available; only write/rm/mv are gated by scopePlanWriteOnly.
                let isWorkReviewFinished (gitCommonDir: string) (sessionId: string) : bool =
                    match Wanxiangshu.Mission.Lifecycle.DurableWorkHost.inspected gitCommonDir sessionId with
                    | Some work when work.ActiveId.IsSome ->
                        ReviewPlanBackend.isReviewFinished gitCommonDir sessionId work.ActiveId.Value
                    | _ -> false

                let planWriteCheck (ctx: HostToolContext) : bool =
                    match cachedGitCommonDir with
                    | Some gitCommonDir when gitCommonDir <> "" -> isWorkReviewFinished gitCommonDir ctx.SessionId
                    | _ -> failwith "Plan draft write gate called without resolved state root"

                let spec =
                    let createSpec =
                        if role = Role.Manager then
                            JsToolSpec.createWithReviewCheck reviewAllowed
                        elif role = Role.Plan then
                            JsToolSpec.createWithPlanWriteCheck planWriteCheck
                        else
                            JsToolSpec.create

                    createSpec
                        factory
                        surface
                        (defaultArg workspaceDirectory "")
                        jsTransactionPersistence
                        jsLifecyclePersistence
                        None
                        jsRootOf
                        settlementOf

                { spec with
                    Execute =
                        fun args ctx ->
                            let envelopeOk (fields: (string * obj) list) : obj = createObj (("ok" ==> true) :: fields)

                            let refusal (code: string) : obj =
                                createObj [ "ok" ==> false; "code" ==> code ]

                            let refusalWithReason (code: string) (reason: string) : obj =
                                createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

                            let envelopeOrRefusal (fields: Result<(string * obj) list, string>) : obj =
                                match fields with
                                | Ok fields -> envelopeOk fields
                                | Error code -> refusal code

                            let placedFields (placed: ForkTool.PlacementReceipt) : (string * obj) list =
                                [ "name" ==> placed.Name
                                  "agentId" ==> placed.AgentId
                                  "outcome" ==> placed.Outcome ]

                            let placedDelegate
                                (execute: _ -> Task<Result<ForkTool.PlacementReceipt, string>>)
                                (payload: obj)
                                : Task<obj> =
                                task {
                                    let! receipt = execute (ToolHostCodec.argumentsOfRaw payload)
                                    return receipt |> Result.map placedFields |> envelopeOrRefusal
                                }

                            let devopsAnswer (outcome: Result<AskResult, string>) : obj =
                                match outcome with
                                | Ok answer ->
                                    envelopeOk
                                        [ "kind" ==> answer.Kind
                                          "status" ==> answer.Status
                                          "body" ==> (ReturnedMaterial.body answer.View |> String.concat "\n")
                                          "returned" ==> ReturnedMaterial.entries answer.View ]
                                | Error reason ->
                                    refusalWithReason "devops-failed" (sprintf "devops failed: %s" reason)

                            let askDevops (payload: obj) (gitCommonDir: string) : Task<obj> =
                                task {
                                    let intentText = devopsIntentText payload
                                    let intent = LlmFacing.instruction intentText |> LlmFacing.render

                                    let delegateTo = devopsDelegateOf syncDelegateRuntime ctx

                                    let! outcome =
                                        Wanxiangshu.Mission.Planning.PlanTools.executeAsk
                                            gitCommonDir
                                            ctx.SessionId
                                            intent
                                            delegateTo

                                    return devopsAnswer outcome
                                }

                            let devopsDelegate (payload: obj) : Task<obj> =
                                task {
                                    let! gitCommonDir = askGitCommonDir workspaceDirectory

                                    if String.IsNullOrWhiteSpace gitCommonDir then
                                        return
                                            refusalWithReason
                                                "devops-failed"
                                                "devops failed: git common directory is missing"
                                    else
                                        return! askDevops payload gitCommonDir
                                }

                            // The commit-side fact of a delivery: the live cell
                            // carries this call's exact fence, so THIS call
                            // established the delivery and the ledger may
                            // record it. A cell of another fence, or no cell
                            // at all, is no delivery of this call.
                            let registerOwnDelivery () =
                                match ctx.ProviderRunId, ctx.ToolCallId with
                                | Some providerRun, Some toolCallId when
                                    JsDeliveredValues.peekFence ctx.SessionId
                                    |> Option.exists (fun fence ->
                                        fence.ProviderRun = providerRun && fence.ToolCallId = toolCallId)
                                    ->
                                    runtime.RegisterEstablishedDelivery(
                                        SessionId.create ctx.SessionId,
                                        providerRun,
                                        toolCallId
                                    )
                                | _ -> ()

                            let host: JsDelegationHost =
                                { Delegate =
                                    fun name payload ->
                                        task {
                                            match name with
                                            | "fork"
                                            | "commission" ->
                                                return! placedDelegate (ForkTool.executeReceipt runtime ctx) payload
                                            | "resume" ->
                                                return!
                                                    placedDelegate (ForkTool.executeResumeReceipt runtime ctx) payload
                                            | "devops" -> return! devopsDelegate payload
                                            | _ -> return refusal "unknown-delegate"
                                        }
                                  Fission =
                                    fun payload ->
                                        task {
                                            // The fission payload is built inside the JS sandbox
                                            // realm, so its prototype is that realm's
                                            // `Object.prototype` — the host's plain-record check
                                            // compares realms and would drop every field. The
                                            // payload is copied into a host-realm plain record
                                            // first, so `charge`, `lanes` and `prompts` survive.
                                            // Fission is not a delegation: it carries no report
                                            // material, so nothing is appended to the copy.
                                            let raw = plainRecordCopy payload

                                            let! receipt =
                                                FissionTool.executeReceipt
                                                    runtime
                                                    ctx
                                                    (ToolHostCodec.argumentsOfRaw raw)

                                            return
                                                match receipt with
                                                | Ok group ->
                                                    envelopeOk
                                                        [ "groupId" ==> group.GroupId
                                                          "lanes"
                                                          ==> (group.Lanes
                                                               |> List.map (fun (index, sessionId) ->
                                                                   createObj
                                                                       [ "index" ==> index
                                                                         "sessionId" ==> sessionId ])
                                                               |> Array.ofList) ]
                                                | Error code -> refusal code
                                        }
                                  Horizon =
                                    fun () ->
                                        let horizonContext: HorizonTool.HorizonRuntimeContext =
                                            { RuntimeFor = runtime.RuntimeFor
                                              LogicalOwnerFor = runtime.LogicalOwnerFor
                                              Journal = runtime.Journal
                                              EnsureWorkDevOpsBound = runtime.EnsureWorkDevOpsBound }

                                        HorizonTool.rosterReceipt
                                            (ProviderLanguage.externalName (
                                                ProviderLanguageBinding.forSessionText ctx.SessionId
                                            ))
                                            horizonContext
                                            ctx
                                  Advisor = fun intent -> advisorConsult runtime ctx intent
                                  Review = fun findings -> reviewMember runtime ctx findings }

                            JsDelegationBridge.install ctx.SessionId host

                            JsDelegationBridge.installAsk ctx.SessionId (fun question ->
                                runtime.AskUser(ctx, question))

                            task {
                                do! ensureStateRoot workspaceDirectory

                                try
                                    let! outcome = spec.Execute args ctx

                                    registerOwnDelivery ()

                                    return outcome
                                finally
                                    JsDelegationBridge.clear ctx.SessionId
                            } })

        let generatedJsSpecs () =
            [ for role in Roles.all do
                  yield! generatedJsSpecFor role |> Option.toList ]

        // Clean-break: the delegate verbs (fork, resume, commission, devops)
        // and the review verb are retired from host registration and live
        // exclusively as programming-surface members. Join, horizon, fission
        // and deliver stay registered: join/horizon/fission are production
        // host tools (not delegates), and deliver is the lifecycle close-out.
        let resolvePlanGitCommonDir () : Task<string option> =
            task {
                match workspaceDirectory with
                | Some ws ->
                    let! root = RuntimePath.stateRoot ws
                    return Some root
                | None -> return None
            }

        let resolvePlanRoot () : Task<string option> =
            task {
                match workspaceDirectory with
                | Some ws ->
                    let! root = RuntimePath.stateRoot ws
                    return Some(root + "/wanxiangshu")
                | None -> return None
            }

        let lifecycleWorkOf
            (runtime: ToolRuntimeScope)
            (sessionId: string)
            : Task<Wanxiangshu.Mission.Lifecycle.DurableWorkHost.InspectedWork option> =
            runtime.LifecycleWorkFor sessionId

        let lifecycleActiveIncumbencyOf (runtime: ToolRuntimeScope) (sessionId: string) : Task<string option> =
            task {
                let! work = lifecycleWorkOf runtime sessionId
                return work |> Option.bind (fun work -> work.ActiveId)
            }

        // work-026: the receipt names the artifact path and the fact that
        // an execution session started; the spawned session id stays out of
        // the provider-visible text.
        let renderDeliverReceipt (res: DeliverResult) : string =
            if res.Relayed then
                res.Message
            elif res.RetirementOutcome = "PostS3" then
                sprintf
                    "%s The plan artifact is ready at %s; nudges are permanently disabled and this session is now in post-S3 state."
                    res.Message
                    res.Path
            elif res.Delivered then
                sprintf "%s The plan artifact is at %s; an execution session has been started." res.Message res.Path
            else
                res.Message

        // work-025/026: the retirement cursor is the exact consumed-through
        // XTrace frontier of the physical exchange that owns this deliver call.
        // The snapshot may already contain a newer user/provider message appended
        // while this deliver tool runs; retirement is fenced to the exact
        // assistant/tool exchange in ctx (its ProviderRunId message and the
        // ToolCallId it answers), never the indiscriminate session head. The
        // whole physical call/result exchange is kept atomic: the frontier lands
        // after the owning assistant message, so the deliver call's own result
        // (appended after retirement) belongs to the successor tenure, and no
        // prior call/result pair is split. A missing snapshot, journal, or
        // owning-exchange identity fails closed with no Plan writes or spawn.
        let deliverPorts (runtime: ToolRuntimeScope) =
            match runtime.Snapshot, runtime.Journal with
            | Some snapshotPort, Some journal -> Ok(snapshotPort, journal)
            | None, _ -> Error "deliver failed: session snapshot is unavailable"
            | _, None -> Error "deliver failed: journal is unavailable"

        let deliverOwnership (ctx: HostToolContext) =
            match ctx.ProviderRunId, ctx.ToolCallId with
            | Some providerRun, Some toolCall -> Ok(ProviderRunIdentity.value providerRun, ToolCallId.value toolCall)
            | None, _ -> Error "deliver failed: owning provider message identity is unavailable"
            | _, None -> Error "deliver failed: owning tool call identity is unavailable"

        let messageOwnsDeliver (message: SessionMessage) (owningCallId: string) =
            message.ToolParts
            |> Array.exists (fun part -> ToolCallId.value part.ToolCallId = owningCallId && part.ToolName = "deliver")

        // Fence to the owning exchange: keep every message up to and including
        // the assistant message that owns this deliver call. The snapshot port
        // pins `order = "asc"` (HOST 1.18.9 session.messages), so the list is
        // already physical append order; it is preserved verbatim. Any newer
        // user/provider message appended while deliver runs stays out of range.
        let tryOwningExchange (messages: SessionMessage list) (owningMessageId: string) (owningCallId: string) =
            let owningIndex =
                messages
                |> List.tryFindIndex (fun (message: SessionMessage) ->
                    message.Id = owningMessageId && message.Role = "assistant")

            match owningIndex with
            | None -> Error "deliver failed: the owning provider message is absent from the session snapshot"
            | Some index when not (messageOwnsDeliver messages.[index] owningCallId) ->
                Error "deliver failed: the owning tool call does not match this deliver invocation"
            | Some index -> Ok(messages.[0..index])

        let readOwningExchange
            (snapshotPort: ISessionSnapshotPort)
            (sessionId: SessionId)
            (owningMessageId: string)
            (owningCallId: string)
            =
            task {
                match! snapshotPort.GetMessages sessionId with
                | Error reason -> return Error(sprintf "deliver failed: cannot read the session snapshot: %s" reason)
                | Ok messages -> return tryOwningExchange messages owningMessageId owningCallId
            }

        let captureFencedFrontier journal sessionId fenced =
            task {
                match!
                    Wanxiangshu.Context.Trace.XTraceCapture.captureSessionMessagesWithReceipt
                        (Some journal)
                        sessionId
                        fenced
                with
                | Ok receipt -> return Ok(Wanxiangshu.Context.Trace.XTraceCursor.sequence receipt.CurrentHead)
                | Error(Wanxiangshu.Context.Trace.XTraceCaptureError.Refused reason) ->
                    return Error(sprintf "deliver failed: XTrace capture refused: %s" reason)
                | Error(Wanxiangshu.Context.Trace.XTraceCaptureError.StorageAppendFailed failure) ->
                    return
                        Error(
                            sprintf
                                "deliver failed: XTrace capture append failed: %s"
                                (JournalOutcome.JournalAppendFailure.describe failure)
                        )
                | Error(Wanxiangshu.Context.Trace.XTraceCaptureError.StorageFailed reason) ->
                    return Error(sprintf "deliver failed: XTrace capture failed: %s" reason)
            }

        let captureOrPropagate journal sessionId =
            function
            | Error err -> Task.FromResult(Error err)
            | Ok fenced -> captureFencedFrontier journal sessionId fenced

        /// The physical input this delivery answers, bound at delivery time.
        ///
        /// The owning assistant message's parent is the physical input only
        /// when that parent is a user message; otherwise the session's current
        /// physical message answers. Both are read here — while the fenced
        /// owning exchange is in hand — and never re-read at registration
        /// time: a registration that re-reads the ambient physical input
        /// would aim this delivery's stop at a later new input.
        let boundPhysicalFor
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (messages: SessionMessage list)
            (owningMessageId: string)
            : PhysicalUserMessageId option =
            let parentBound =
                messages
                |> List.tryFind (fun message -> message.Id = owningMessageId && message.Role = "assistant")
                |> Option.bind (fun message -> physicalUserMessageOf messages message None)

            parentBound
            |> Option.orElseWith (fun () ->
                runtime.CurrentPhysicalUserMessage ctx.SessionId
                |> Option.map PhysicalUserMessageId.create)

        let resolveDeliverRetirementCursor
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            : Task<Result<int64 * PhysicalUserMessageId option, string>> =
            task {
                match deliverPorts runtime, deliverOwnership ctx with
                | Ok(snapshotPort, journal), Ok(owningMessageId, owningCallId) ->
                    let! exchange =
                        readOwningExchange snapshotPort (SessionId.create ctx.SessionId) owningMessageId owningCallId

                    let boundPhysical =
                        exchange
                        |> Result.map (fun messages -> boundPhysicalFor runtime ctx messages owningMessageId)
                        |> Result.defaultValue None

                    let! cursor = captureOrPropagate journal (SessionId.create ctx.SessionId) exchange

                    return cursor |> Result.map (fun value -> value, boundPhysical)
                | Error err, _ -> return Error err
                | _, Error err -> return Error err
            }

        let deliverCreate (runtime: ToolRuntimeScope) (operationId: string) (agent: string) =
            runtime.CreateExecutionSession(operationId, agent)

        let deliverPrompt (runtime: ToolRuntimeScope) (execSessionId: string) (agent: string) (prompt: string) =
            runtime.PromptExecutionSession(execSessionId, agent, prompt)

        let deliverDiscard (runtime: ToolRuntimeScope) (execSessionId: string) =
            runtime.DiscardExecutionSession execSessionId

        let renderDeliverOutcome outcome =
            match outcome with
            | Ok res -> renderDeliverReceipt res
            | Error err -> sprintf "deliver failed: %s" err

        /// 条款 3/4/6: 交付已成立是提交面事实, 登记精确 attempt 围栏与交付当时
        /// 绑定的物理输入。登记时刻绝不重读会话的当前物理输入——那正是把本次
        /// 交付的停止瞄准后来新输入的窗口。绑定不出来的交付不登记: 无法证明
        /// 成立就不是已成立。
        let registerEstablishedDelivery
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (boundPhysical: PhysicalUserMessageId option)
            (outcome: Result<DeliverResult, string>)
            =
            let established =
                match outcome with
                | Ok res -> res.RetirementOutcome = "PostS3" || res.RetirementOutcome = "Delivered"
                | Error _ -> false

            match established, boundPhysical, ctx.ProviderRunId, ctx.ToolCallId with
            | true, Some physical, Some runId, Some callId ->
                runtime.RegisterEstablishedDelivery(
                    SessionId.create ctx.SessionId,
                    runId,
                    callId,
                    physicalUserMessageId = physical
                )
            | _ -> ()

        let deliverCursor
            (root: string)
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (incumbencyId: string)
            (gitCommonDir: string)
            : Task<string> =
            task {
                let providerRunId =
                    ctx.ProviderRunId
                    |> Option.map ProviderRunIdentity.value
                    |> Option.defaultValue ""

                let toolCallId =
                    ctx.ToolCallId |> Option.map ToolCallId.value |> Option.defaultValue ""

                let retirementBlockers () =
                    runtime.RetirementBlockersFor ctx.SessionId |> List.toArray

                match! resolveDeliverRetirementCursor runtime ctx with
                | Error err -> return sprintf "deliver failed: %s" err
                | Ok(retirementCursor, boundPhysical) ->
                    let! outcome =
                        PlanTools.enterPostS3OrRelay
                            gitCommonDir
                            root
                            ctx.SessionId
                            incumbencyId
                            providerRunId
                            toolCallId
                            retirementBlockers
                            retirementCursor

                    registerEstablishedDelivery runtime ctx boundPhysical outcome

                    return renderDeliverOutcome outcome
            }

        let deliverPostS3
            (root: string)
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (gitCommonDir: string)
            : Task<string> =
            task {
                let providerRunId =
                    ctx.ProviderRunId
                    |> Option.map ProviderRunIdentity.value
                    |> Option.defaultValue ""

                let toolCallId =
                    ctx.ToolCallId |> Option.map ToolCallId.value |> Option.defaultValue ""

                let retirementBlockers () =
                    runtime.RetirementBlockersFor ctx.SessionId |> List.toArray

                match! resolveDeliverRetirementCursor runtime ctx with
                | Error err -> return sprintf "deliver failed: %s" err
                | Ok(retirementCursor, boundPhysical) ->
                    let! outcome =
                        PlanTools.startPostS3Execution
                            gitCommonDir
                            root
                            ctx.SessionId
                            providerRunId
                            toolCallId
                            retirementBlockers
                            (deliverCreate runtime)
                            (deliverPrompt runtime)
                            (deliverDiscard runtime)
                            retirementCursor

                    registerEstablishedDelivery runtime ctx boundPhysical outcome

                    return renderDeliverOutcome outcome
            }

        let deliverForIncumbency
            (root: string)
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (incumbencyId: string)
            : Task<string> =
            task {
                let! gitCommonDir =
                    task {
                        match runtime.WorkspaceDirectory with
                        | Some ws -> return! Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot ws
                        | None -> return ""
                    }

                if String.IsNullOrWhiteSpace gitCommonDir then
                    return "deliver failed: git common directory is missing"
                else
                    return! deliverCursor root runtime ctx incumbencyId gitCommonDir
            }

        let runDeliver (root: string) (runtime: ToolRuntimeScope) (ctx: HostToolContext) : Task<string> =
            task {
                let! gitCommonDir =
                    task {
                        match runtime.WorkspaceDirectory with
                        | Some ws -> return! Wanxiangshu.Persistence.Journal.RuntimePath.stateRoot ws
                        | None -> return ""
                    }

                match! lifecycleWorkOf runtime ctx.SessionId with
                | Some work when work.PendingDelivery && work.Sequence = [ "S1"; "S2"; "S3" ] ->
                    return! deliverPostS3 root runtime ctx gitCommonDir
                | Some work when work.ActiveId.IsSome ->
                    return! deliverForIncumbency root runtime ctx work.ActiveId.Value
                | Some work when
                    work.Delivered
                    && work.Sequence = [ "S1"; "S2"; "S3" ]
                    && String.IsNullOrWhiteSpace gitCommonDir
                    ->
                    return "deliver failed: git common directory is missing"
                | Some work when work.Delivered && work.Sequence = [ "S1"; "S2"; "S3" ] ->
                    return! deliverPostS3 root runtime ctx gitCommonDir
                | _ -> return "deliver failed: no active incumbency found for this work"
            }

        let executeDeliverTool
            (rootOpt: string option)
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            : Task<string> =
            task {
                match rootOpt with
                | Some root -> return! runDeliver root runtime ctx
                | None -> return "deliver failed: the plan workspace is unavailable"
            }


        let deliverSpec () =
            let managerSpec = DeliverTool.spec factory runtime

            let planSpec =
                { Name = "deliver"
                  Description = ProviderProse.render providerLanguage "tool/deliver" Map.empty
                  Arguments = [ "note", ToolHostCodec.optionalStringSchema factory ]
                  Execute =
                    fun _ ctx ->
                        task {
                            let! root = resolvePlanRoot ()
                            return! executeDeliverTool root runtime ctx
                        } }

            let sequenceOf (ctx: HostToolContext) : Task<string list option> =
                task {
                    let! work = lifecycleWorkOf runtime ctx.SessionId
                    return work |> Option.map (fun work -> work.Sequence)
                }

            let executeDeliverRoute (ctx: HostToolContext) (args: HostToolArguments) : Task<string> =
                task {
                    match! sequenceOf ctx with
                    | Some [ "S1"; "S2"; "S3" ] -> return! planSpec.Execute args ctx
                    | _ -> return! managerSpec.Execute args ctx
                }

            { managerSpec with
                Execute =
                    fun args ctx ->
                        task {
                            // Algorithm G1: the deliver gate based on real resource ownership.
                            // Pending named programs must be resumed or abandoned before delivery.
                            let blockers = JsPrograms.deliveryBlockersWithChoicesForSession ctx.SessionId

                            if not (List.isEmpty blockers) then
                                let handles = blockers |> List.map fst |> String.concat ", "
                                let reasons = blockers |> List.map snd |> String.concat "; "

                                return
                                    ToolResultText.tomlObjectWithInstructions
                                        [ ProviderProse.render
                                              (lang ctx)
                                              Path.DeliverBlockedByParked
                                              (Map [ "handles", handles; "reasons", reasons ]) ]
                                        [ "code", ToolResultText.TString Path.DeliverBlockedByParked
                                          "program_names", ToolResultText.TString handles
                                          "reasons", ToolResultText.TString reasons ]
                            else
                                return! executeDeliverRoute ctx args
                        } }

        let deliverTool = deliverSpec ()

        let recordedDeliverReplay (ctx: HostToolContext) : Task<bool> =
            task {
                match ctx.ProviderRunId, ctx.ToolCallId with
                | Some run, Some call ->
                    let settled work =
                        DeliverTool.recordedFenceReplay work (ProviderRunIdentity.value run) (ToolCallId.value call)
                        |> Option.isSome

                    let! work = lifecycleWorkOf runtime ctx.SessionId
                    return work |> Option.exists settled
                | _ -> return false
            }

        let negotiateSpec: ToolSpec =
            { Name = "negotiate"
              Description =
                "Ask your delegator clarifying questions. The first call is required before resetScope initialization; later calls are optional and can happen at any time. Pass all questions together in questions (plural). The call immediately acknowledges sending and does not wait for an answer. The answer later arrives as a user steer and does not block ongoing work."
              Arguments =
                [ "questions",
                  ToolHostCodec.stringSchemaDescribed
                      "Ask all clarifying questions in this single argument, verbosely enough to avoid ambiguity. Several related questions are welcome."
                      factory ]
              Execute = fun args ctx -> Task.FromResult(runtime.Negotiate(ctx.SessionId, args.Text "questions")) }

        let previousScopeSpec: ToolSpec =
            { Name = "askPreviousScope"
              Description =
                "Ask the immediately preceding Scope concrete clarifying questions using the single questions argument. Accepted questions run asynchronously; the answer arrives as an agent-attributed steer. Follow-up questions are supported."
              Arguments =
                [ "questions",
                  ToolHostCodec.stringSchemaDescribed
                      "Ask your previous Scope questions based on its handoff and the current objective. Group related questions in one call."
                      factory ]
              Execute = fun args ctx -> runtime.AskPreviousScope(ctx, args.Text "questions") }

        let baseSpecs =
            [ yield negotiateSpec
              yield previousScopeSpec
              yield JoinTool.spec runtime

              let horizonContext: HorizonTool.HorizonRuntimeContext =
                  { RuntimeFor = runtime.RuntimeFor
                    LogicalOwnerFor = runtime.LogicalOwnerFor
                    Journal = runtime.Journal
                    EnsureWorkDevOpsBound = runtime.EnsureWorkDevOpsBound }

              yield HorizonTool.spec horizonContext
              yield FissionTool.spec factory runtime
              yield deliverTool
              yield BashHoneypotTool.spec
              yield AssumeTool.spec factory
              yield ResetScopeTool.spec factory runtime.Journal

              yield! casebookToolSpecs
              yield! generatedJsSpecs () ]

        let gateFacts: ToolGateFacts =
            { RoleFor = (fun (ctx: HostToolContext) -> runtime.RoleFor ctx)
              EnsureRoleFor = (fun (ctx: HostToolContext) -> runtime.EnsureRoleFor ctx)
              ManagerFactsFor = (fun sessionId -> runtime.ManagerCapabilityFactsFor sessionId)
              IsRetirementFrozen = (fun sessionId -> runtime.IsRetirementFrozen sessionId)
              IsWaitingForUser = runtime.IsWaitingForUser
              WaitingQuestionFenceFor = runtime.WaitingQuestionFenceFor
              BeginToolExecution = beginToolExecution
              EndToolExecution = endToolExecution
              RecordedDeliverReplay = recordedDeliverReplay }

        let gateExecute (spec: Wanxiangshu.OpenCode.ToolSpec) =
            let managerPermission =
                match ManagerReviewTools.requiredPermissions spec.Name with
                | Some perms -> perms |> Seq.tryHead
                | None -> StaticTools.permissionOfToolName Role.Manager spec.Name

            let gated: GatedTool<HostToolArguments> =
                { Name = spec.Name
                  Execute = spec.Execute }

            fun args (ctx: HostToolContext) -> ToolExecuteGate.execute gateFacts gated managerPermission args ctx

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
