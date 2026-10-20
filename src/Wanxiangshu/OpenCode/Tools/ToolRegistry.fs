namespace Wanxiangshu.OpenCode

open System
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Planning
open Wanxiangshu.Mission.Work.OpenCode
open Wanxiangshu.Mission.Work
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.Anchor
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Ablation
open Wanxiangshu.Process

/// Assembly-only registry: tool behavior lives in one vertical verb module;
/// per-session resources live in ToolRuntimeScope.
type ToolRegistration =
    { Tools: obj
      Runtime: ToolRuntimeScope }

module ToolRegistry =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        let DeniedRole = "tool/registry/denied-role"

        /// Fission is Engineer-only; its denial names the sole entitled role.
        [<Literal>]
        let DeniedFission = "tool/registry/denied-fission"

        [<Literal>]
        let DeniedAblation = "tool/registry/denied-ablation"

        [<Literal>]
        let DeniedStrength = "tool/registry/denied-strength"

        [<Literal>]
        let DeniedUnestablished = "tool/registry/denied-unestablished"

        [<Literal>]
        let DeniedTaskState = "tool/registry/denied-task-state"

        /// Algorithm G1: deliver is blocked while any parked site is unresolved.
        [<Literal>]
        let DeliverBlockedByParked = "tool/js-program/parked-deliver-blocked"

    let private lang (ctx: HostToolContext) =
        let sessionText = ctx.SessionId

        ProviderLanguageBinding.forSessionText sessionText

    /// The refusal prose follows the provider language, so it cannot serve as a
    /// machine-readable reason. `code` carries the resource key when a caller
    /// needs one; omitting it leaves the rendered bytes exactly as before, which
    /// is why tools without a code contract keep prose only.
    let private denied
        (ctx: HostToolContext)
        (specName: string)
        (path: string)
        (subs: Map<string, string>)
        (code: string option)
        =
        let fields =
            match code with
            | Some key -> [ "code", ToolHostCodec.TString key ]
            | None -> []

        ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] fields

    let private denyRole (ctx: HostToolContext) (specName: string) (role: Role) =
        let path =
            if specName = "fission" then
                Path.DeniedFission
            else
                Path.DeniedRole

        denied ctx specName path (Map [ "tool", specName; "role", sprintf "%A" role ]) None

    /// The current capability decides; the denial stays action-focused and never
    /// echoes internal loop state.
    let private denyTaskState (ctx: HostToolContext) (specName: string) =
        denied ctx specName Path.DeniedTaskState (Map [ "tool", specName ]) None

    /// execution-model-routing-010: the tool context carries this call's own
    /// exact ProviderRunIdentity. The physical message it answers is the one the
    /// Host stated for that run; a run this process never observed has no step to
    /// end, and must never be resolved from the session's current binding.
    let private endObservedStep (providerRun: ProviderRunIdentity) =
        ModelRouting.tryProviderStepIdentity providerRun
        |> Option.iter (fun (sessionId, physicalUserMessageId) ->
            ModelRouting.endProviderStep sessionId physicalUserMessageId providerRun)

    let private providerToolBoundary (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            Ok()
        else
            ctx.ProviderRunId |> Option.iter endObservedStep
            Ok()

    /// capability-enforcement-005/010/025: the exact session facts the execute
    /// gate reads. `create` fills every field from the ToolRuntimeScope or its
    /// own callbacks; a stage never receives the whole scope.
    type private ToolGateFacts =
        {
            RoleFor: HostToolContext -> Role option
            EnsureRoleFor: HostToolContext -> Task<Role option>
            ManagerFactsFor: string -> Task<ManagerCapabilityFacts>

            IsRetirementFrozen: string -> Task<bool>
            IsWaitingForUser: string -> bool
            /// LC-017: the pending question's exact fence (provider run, tool call)
            /// for a session, when a question is waiting for the user. Ask replays
            /// carrying the same fence pass the waiting gate; everything else
            /// stays refused.
            WaitingQuestionFenceFor: string -> (ProviderRunIdentity * ToolCallId) option
            BeginToolExecution: string -> unit
            EndToolExecution: string -> unit
            /// Algorithm E1.1: the work already recorded a retirement for this
            /// call's exact fence, so a retried deliver replays its state instead
            /// of being refused for the tenure that retirement removed.
            RecordedDeliverReplay: HostToolContext -> Task<bool>
        }

    let private accountingStageWithSession
        (gate: ToolGateFacts)
        (ctx: HostToolContext)
        (run: unit -> Task<string>)
        : Task<string> =
        task {
            gate.BeginToolExecution ctx.SessionId

            try
                return! run ()
            finally
                gate.EndToolExecution ctx.SessionId
        }

    /// Tool-execution accounting around one physical gate pass.
    let private accountingStage
        (gate: ToolGateFacts)
        (ctx: HostToolContext)
        (run: unit -> Task<string>)
        : Task<string> =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            run ()
        else
            accountingStageWithSession gate ctx run

    /// feature-ablation-002: an ablated node never reaches the tool body.
    let private ablationStage (spec: ToolSpec) (ctx: HostToolContext) : Task<string option> =
        Task.FromResult(
            if AblationGate.toolDenied (AblationGate.registry ()) spec.Name then
                Some(denied ctx spec.Name Path.DeniedAblation (Map [ "tool", spec.Name ]) None)
            else
                None
        )



    /// capability-enforcement-025: current Manager facts close the office gate.
    /// A frozen retirement keeps Join, Finality and the explicit human Ask; the final incumbent
    /// keeps the read/cleanup/close-out surface and loses every new-work
    /// capability.
    /// Algorithm E1.1: a retried deliver whose fence the work already settled is
    /// not a new capability — it replays the state that call recorded, so the
    /// missing tenure is not a reason to refuse it. Every other denied call
    /// keeps the action-focused refusal.
    let private executeDeliverReplay
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            let! replayed = gate.RecordedDeliverReplay ctx

            if replayed then
                return! spec.Execute args ctx
            else
                return denyTaskState ctx spec.Name
        }

    let private refusedOrReplayed
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        if spec.Name = "deliver" then
            executeDeliverReplay gate spec args ctx
        else
            Task.FromResult(denyTaskState ctx spec.Name)

    let private managerFactsStage
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            let! managerFacts = gate.ManagerFactsFor ctx.SessionId
            let! frozen = gate.IsRetirementFrozen ctx.SessionId

            match managerPermission with
            | Some permission when
                frozen
                && permission <> ToolPermission.Join
                && permission <> ToolPermission.Finality
                ->
                return denyTaskState ctx spec.Name
            | Some permission when not (OfficeCapability.isAllowedForManagerFacts managerFacts permission) ->
                return! refusedOrReplayed gate spec args ctx
            | _ -> return! spec.Execute args ctx
        }

    let private executeAdmittedRole
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        (role: Role)
        : Task<string> =
        let isExactQuestionReplay =
            match ctx.ProviderRunId, ctx.ToolCallId with
            | Some run, Some call -> gate.WaitingQuestionFenceFor ctx.SessionId = Some(run, call)
            | _ -> false

        let isWaitingReplayExempt =
            (spec.Name = "deliver" || spec.Name.StartsWith "todowrite-for-")
            && isExactQuestionReplay

        if gate.IsWaitingForUser ctx.SessionId && not isWaitingReplayExempt then
            Task.FromResult(
                ProviderProse.render (ProviderLanguageBinding.forSessionText ctx.SessionId) "tool/ask-waiting" Map.empty
            )
        elif role = Role.Manager && spec.Name <> "todowrite-for-manager" then managerFactsStage gate spec managerPermission args ctx
        else
            spec.Execute args ctx

    /// capability-enforcement-002: office Role then admission then Manager facts.
    let private officeStage
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (officeAdmission: HostToolContext -> Role -> bool)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            let! resolvedRole =
                match gate.RoleFor ctx with
                | Some role -> Task.FromResult(Some role)
                | None -> gate.EnsureRoleFor ctx

            match resolvedRole with
            | Some role when officeAdmission ctx role ->
                return! executeAdmittedRole gate spec managerPermission args ctx role
            | Some role -> return denyRole ctx spec.Name role
            | None -> return denied ctx spec.Name Path.DeniedUnestablished Map.empty None
        }

    /// capability-enforcement-006: the attachment IS the authority. Resolving a
    /// public office Role here would deny the Bookkeeper its own exact tool,
    /// because a HostInternal prompt deliberately installs no public authority
    /// profile.
    let private attachmentStage
        (spec: ToolSpec)
        (attachmentAdmission: HostToolContext -> bool)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            if attachmentAdmission ctx then
                return! spec.Execute args ctx
            else
                return denied ctx spec.Name Path.DeniedUnestablished Map.empty None
        }

    let private admissionStage
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        match spec.Admission with
        | ToolAdmission.OfficeRole officeAdmission -> officeStage gate spec managerPermission officeAdmission args ctx
        | ToolAdmission.PrivateAttachment attachmentAdmission -> attachmentStage spec attachmentAdmission args ctx

    let private runAfterAblation
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        admissionStage gate spec managerPermission args ctx


    let private runBoundaryStages
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            match! ablationStage spec ctx with
            | Some refusal -> return refusal
            | None -> return! runAfterAblation gate spec managerPermission args ctx
        }

    /// The one composition point. Six named stages, fixed ordinary calls in the
    /// order written here. No middleware list, no fold, no registry.
    let private executeGate
        (gate: ToolGateFacts)
        (spec: ToolSpec)
        (managerPermission: ToolPermission option)
        (args: HostToolArguments)
        (ctx: HostToolContext)
        : Task<string> =
        task {
            match providerToolBoundary ctx with
            | Error error -> return raise (InvalidOperationException error)
            | Ok() ->
                return! accountingStage gate ctx (fun () -> runBoundaryStages gate spec managerPermission args ctx)
        }

    let private staticAdmissions () : (string * ToolAdmission) list =
        [ "join", JoinTool.admission
          "horizon", HorizonTool.admission
          "fission", FissionTool.admission
          "review", ReviewShell.admission
          "deliver",
          ToolAdmission.OfficeRole(fun _ role ->
              OfficeCapability.isAllowed role ToolPermission.Finality
              || OfficeCapability.isAllowed role ToolPermission.Deliver)
          "bash-honeypot", BashHoneypotTool.admission
          "assume", AssumeTool.admission
          "fetch", ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fetch)
          "todowrite-for-bookkeeper", JsBookkeeperTool.admission ]

    let private tryAdmissionFor (specName: string) : ToolAdmission option =
        match staticAdmissions () |> List.tryFind (fun (name, _) -> name = specName) with
        | Some(_, admission) -> Some admission

        | None when specName.StartsWith "todowrite-for-" && specName <> "todowrite-for-bookkeeper" ->
            Some(JsToolSpec.admissionFor (specName.Substring 14))
        | None -> None

    let private probeContext (sessionId: string) : HostToolContext =
        { SessionId = sessionId
          Agent = None
          ToolCallId = None
          ProviderRunId = None
          PromptText = None
          AttachAbort = fun _ -> id }

    /// capability-enforcement-006: the authority the execute gate resolves for a tool, so a
    /// consumer can tell an office tool from an internal leaf without guessing
    /// from the tool name.
    let tryAdmission (specName: string) : ToolAdmission option = tryAdmissionFor specName

    /// capability-enforcement-006: the internal-leaf decision for a session that holds no public
    /// office profile at all. An office tool is never admitted this way.
    let privateAttachmentAdmits (specName: string) (sessionId: string) : bool =
        match tryAdmissionFor specName with
        | Some(ToolAdmission.PrivateAttachment predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.OfficeRole _)
        | None -> false

    /// AGENT-007 role gate, delegates to owner-defined tool admissions.
    /// sessionId is the tool call's Host session.
    let rolePredicate (specName: string) (sessionId: string) : Role -> bool =
        // capability-enforcement-006: an internal leaf tool is admitted by attachment, never by a
        // public office, so no public Role may ever see it on this surface.
        match tryAdmissionFor specName with
        | Some(ToolAdmission.OfficeRole predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.PrivateAttachment _)
        | None -> fun _ -> false



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

    /// The report material a delegate receives: the caller's report mapping
    /// rendered as `<anchor>: <text>` lines, one per entry, newline-joined.
    /// No entries means no material at all.
    let private reportMaterialText (sessionId: string) (mapping: obj) : Result<string option, JsFailure> =
        AnchorSurface.reportMaterial sessionId mapping
        |> Result.map (fun entries ->
            entries
            |> List.map (fun (anchor, text) -> sprintf "%d: %s" anchor text)
            |> fun lines ->
                if List.isEmpty lines then
                    None
                else
                    Some(String.concat "\n" lines))

    /// The delegate payload keeps every field the program sent and gains the
    /// report material under `commissioner_record`. The payload is normalized
    /// into a host-realm record first, so a payload built in the sandbox
    /// realm is not dropped by the host's plain-record check.
    let private payloadWithRecord (payload: obj) (record: string option) : obj =
        let raw =
            if isStringValue payload then
                createObj [ "charge" ==> payload ]
            elif isNullish payload then
                createObj []
            else
                plainRecordCopy payload

        match record with
        | None -> raw
        | Some text -> withField raw "commissioner_record" (box text)

    /// The devops intent travels as the program's own text: a plain string
    /// payload is the intent itself, an object payload carries it under
    /// `intent`. The object form is read from the normalized host-realm
    /// record, so the intent of a sandbox-built payload is not lost.
    let private devopsIntentText (payload: obj) (normalizedPayload: obj) : string =
        if isStringValue payload then
            unbox<string> payload
        else
            (ToolHostCodec.argumentsOfRaw normalizedPayload).Text "intent"

    /// The Plan programming surface runs under one exact call: the provider
    /// run that asked for it and the tool call that carries it. A delivery
    /// the program makes is evidence only when it carries that fence, so the
    /// fence travels with the execution and is handed to the workflow as-is.
    /// Without a fence the delivery lands as a render cache — presentable,
    /// but never a delivery.
    let runPlanProgram
        (prose: JsCanonicalDescription.Prose)
        (gitCommonDir: string)
        (root: string)
        (sessionId: string)
        (program: string)
        (fence: (ProviderRunIdentity * ToolCallId) option)
        : Task<string> =
        task {
            // Plan is unified: uses the standard JsToolWorkflow.execute with
            // the Plan role's projected capabilities. The working directory
            // is the plan work's assigned directory (root/plan/sessionId).
            let planRoot =
                match Wanxiangshu.Mission.Planning.PlanPath.resolvePlanPath root sessionId with
                | Ok fullPath ->
                    let idx = fullPath.LastIndexOf('/')
                    if idx >= 0 then fullPath.Substring(0, idx) else root
                | Error _ -> root

            let capabilities =
                JsCapability.ofToolCapabilities (OfficeCapability.permissions Role.Plan)
            let baseClass = JsCanonicalDescription.runtimeBaseClass prose capabilities
            let deadlineEpochMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000L
            let input = createObj [ "program" ==> program ]
            let! outcome =
                JsToolWorkflow.execute
                    sessionId
                    fence
                    capabilities
                    planRoot
                    baseClass
                    input
                    10000
                    deadlineEpochMs
                    (1 <<< 20)
                    None
                    None
                    None
            return JsToolsResult.render outcome
        }

    /// The advisor consult accepts the report mapping only — a mapping of
    /// anchor numbers to summaries, equivalent to a `prefixUntil = 0`
    /// declaration. There is no lenient empty declaration.
    let private advisorDeclarationOf (reportMapping: obj) =
        AnchorSurface.parseReportMapping reportMapping

    /// The transform's own wire text over a decoded snapshot part: the advisor
    /// reads exactly what the caller's request carried, tool calls included.
    let private advisorPartText (part: MessagePart) : string =
        match part with
        | MessagePart.Text text
        | MessagePart.Reasoning text -> text
        | MessagePart.ToolCall(_, name, args) -> name + " " + args
        | MessagePart.ToolResult(_, result) -> result
        | MessagePart.Activity _ -> ""

    let private advisorMessageText (parts: MessagePart array) : string =
        if isNull parts then
            ""
        else
            parts |> Array.map advisorPartText |> String.concat "\n"

    /// Numbering each snapshot message is what keeps the anchors in a consult
    /// identical to the ones the caller's own request used.
    let private advisorViewMessage (sessionId: string) (message: SessionMessage) : AdvisorViewMessage =
        { Anchor = AnchorLedger.numberMessage sessionId message.Id
          Text = advisorMessageText message.Parts }

    let private advisorIntentText (intent: obj) : string =
        if isStringValue intent then unbox<string> intent else ""

    /// advisor-[006]: the member's refusals are data — `{ ok = false, code,
    /// reason }` — never a throw the program would have to catch.
    let private advisorRefusal (code: string) (reason: string) : obj =
        createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

    /// advisor-[002]/[003]: one consult — the caller's own delta rendered as the
    /// seat's first prompt, the intent appended after it, and the seat's answer
    /// handed back as data. A successful consult advances that caller session's
    /// delivered-anchor cursor; a refusal changes nothing.
    let private advisorSeatAnswer
        (runtime: ToolRuntimeScope)
        (ctx: HostToolContext)
        (messages: SessionMessage list)
        (declaration: AdvisorViewDeclaration)
        (intent: obj)
        : Task<obj> =
        task {
            let sessionId = ctx.SessionId

            let request: AdvisorViewRequest =
                { Messages = messages |> List.map (advisorViewMessage sessionId)
                  Declaration = declaration
                  History = AnchorLedger.replacements sessionId }

            let plan =
                AdvisorView.plan (ProviderLanguage.label (lang ctx)) (Cursor.delivered sessionId) request

            let deps: AdvisorSeatDeps =
                { Sessions = runtime.Sessions
                  Snapshot = runtime.Snapshot
                  Journal = runtime.Journal
                  Directory = runtime.WorkspaceDirectory
                  Participant = ManagedAgent.nameOf Role.Advisor }

            let! answered = AdvisorSeat.consult deps sessionId (advisorIntentText intent) plan.ViewPrompt

            match answered with
            | Ok advice ->
                Cursor.record sessionId plan.DeliveredMaterial
                return createObj [ "ok" ==> true; "advice" ==> advice ]
            | Error reason -> return advisorRefusal "advisor-unavailable" reason
        }

    /// advisor-[006]: the `js.advisor` host closure. The caller's context comes
    /// from its own session snapshot; an unreadable snapshot or an invalid view
    /// declaration is a refusal, never a silently empty view. `HasNewMaterial`
    /// does not gate the consult: the intent alone is new material.
    let private advisorSnapshotMessages
        (runtime: ToolRuntimeScope)
        (ctx: HostToolContext)
        : Task<Result<SessionMessage list, string>> =
        match runtime.Snapshot with
        | None -> Task.FromResult(Error "the session snapshot is unavailable")
        | Some snapshotPort -> snapshotPort.GetMessages(SessionId.create ctx.SessionId)

    let private advisorConsult
        (runtime: ToolRuntimeScope)
        (ctx: HostToolContext)
        (intent: obj)
        (reportMapping: obj)
        : Task<obj> =
        task {
            let! loaded = advisorSnapshotMessages runtime ctx

            match loaded, advisorDeclarationOf reportMapping with
            | Error reason, _ -> return advisorRefusal "advisor-unavailable" reason
            | _, Error failure -> return advisorRefusal (JsFailure.code failure) (JsFailure.reason failure)
            | Ok messages, Ok declaration -> return! advisorSeatAnswer runtime ctx messages declaration intent
        }

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
            JsToolSpec.rootForSession sessionDirectories (defaultArg workspaceDirectory "") sessionId

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
        /// A parent that is not a user message is not a physical input, so the
        /// session's current physical message answers instead.
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
                currentPhysical |> Option.map PhysicalUserMessageId.create

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
                        DeliveryTaskReference.Lifecycle(
                            SessionId.value sessionId,
                            retirement.IncumbencyId
                        ),
                        Some retirement.RetirementId))

            let childHandle =
                (AgentJournal.snapshot durable).AgentProjections.HandleByChildSession
                |> Map.tryFind sessionId
                |> Option.bind (fun record ->
                    let agentHandle =
                        HandleId.tryAgent record.Handle
                        |> Option.map AgentHandleId.value
                        |> Option.filter (fun value -> not (String.IsNullOrWhiteSpace value))

                    match agentHandle with
                    | Some handle -> Some handle
                    | None ->
                        if String.IsNullOrWhiteSpace record.TargetAgent then
                            None
                        else
                            Some record.TargetAgent)

            match lifecycleTask, childHandle with
            | Some task, _ -> task
            | None, Some handle -> DeliveryTaskReference.ChildWork handle, None
            | None, None -> DeliveryTaskReference.Invocation toolCallIdText, None

        /// The delivery settlement of one js-* call: the host's attribution of
        /// the call and the durable place its witness goes.
        ///
        /// Every field is read from the source the read side reads. A field
        /// that cannot be read leaves the call without a settlement: no
        /// witness is invented, and the call keeps its ordinary behaviour.
        let settlementOf (ctx: HostToolContext) : Task<DeliverySettlement option> =
            match journal, runtime.Snapshot, ctx.ProviderRunId, ctx.ToolCallId with
            | Some durable, Some snapshotPort, Some providerRun, Some toolCallId ->
                let sessionId = SessionId.create ctx.SessionId

                let activeProfile =
                    Wanxiangshu.Interaction.Authority.PromptAuthorityProjectionQueries.activeProfile
                        sessionId
                        (AgentJournal.snapshot durable).AgentProjections

                match activeProfile with
                | None -> Task.FromResult None
                | Some profile ->
                    let logicalRunId = profile.LogicalRunId
                    let authorityRoot = profile.AuthorityRootUserMessageId

                    if String.IsNullOrWhiteSpace(LogicalRunId.value logicalRunId)
                       || String.IsNullOrWhiteSpace(AuthorityRootUserMessageId.value authorityRoot) then
                        Task.FromResult None
                    else
                        task {
                            let! loaded = snapshotPort.GetMessages sessionId

                            match loaded with
                            | Error _ -> return None
                            | Ok messages ->
                                match owningAssistantMessage messages providerRun with
                                | None -> return None
                                | Some message ->
                                    match formalTextOf message with
                                    | None -> return None
                                    | Some formalText ->
                                        let physical =
                                            physicalUserMessageOf
                                                messages
                                                message
                                                (currentPhysicalUserMessage ctx.SessionId)

                                        match physical with
                                        | None -> return None
                                        | Some physical ->
                                            // The lifecycle work is read only for a
                                            // call that already has its attribution:
                                            // the work journal is a durable read and
                                            // must not be paid for calls that never
                                            // get a witness.
                                            let! work = runtime.LifecycleWorkFor ctx.SessionId

                                            let deliveryTask, retirementId =
                                                deliveryTaskOf
                                                    durable
                                                    sessionId
                                                    providerRun
                                                    toolCallId
                                                    work

                                            return
                                                Some
                                                    { Attribution =
                                                        { LogicalRunId = logicalRunId
                                                          AuthorityRoot = authorityRoot
                                                          PhysicalUserMessageId = physical
                                                          Task = deliveryTask
                                                          RetirementId = retirementId
                                                          FormalText = formalText }
                                                      Writer = DeliveryWitnessWriter.create durable }
                        }
            | _ -> Task.FromResult None

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
                            && not facts.HasConfirmation
                            && facts.CleanupBlockerDigest.IsNone
                            && not frozen
                    }

                let spec =
                    let createSpec =
                        if role = Role.Manager then
                            JsToolSpec.createWithReviewCheck reviewAllowed
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
                            let envelopeOk (fields: (string * obj) list) : obj =
                                createObj (("ok" ==> true) :: fields)

                            let refusal (code: string) : obj =
                                createObj [ "ok" ==> false; "code" ==> code ]

                            let refusalWithReason (code: string) (reason: string) : obj =
                                createObj [ "ok" ==> false; "code" ==> code; "reason" ==> reason ]

                            let host: JsDelegationHost =
                                { Delegate =
                                    fun name payload mapping ->
                                        task {
                                            let material = reportMaterialText ctx.SessionId mapping

                                            match name with
                                            | "fork" | "commission" ->
                                                match material with
                                                | Error failure ->
                                                    return
                                                        refusalWithReason
                                                            (JsFailure.code failure)
                                                            (JsFailure.reason failure)
                                                | Ok record ->
                                                    let raw = payloadWithRecord payload record

                                                    let! receipt =
                                                        ForkTool.executeReceipt
                                                            runtime
                                                            ctx
                                                            (ToolHostCodec.argumentsOfRaw raw)

                                                    return
                                                        match receipt with
                                                        | Ok placed ->
                                                            envelopeOk
                                                                [ "name" ==> placed.Name
                                                                  "agentId" ==> placed.AgentId
                                                                  "outcome" ==> placed.Outcome ]
                                                        | Error code -> refusal code
                                            | "resume" ->
                                                match material with
                                                | Error failure ->
                                                    return
                                                        refusalWithReason
                                                            (JsFailure.code failure)
                                                            (JsFailure.reason failure)
                                                | Ok record ->
                                                    let raw = payloadWithRecord payload record

                                                    let! receipt =
                                                        ForkTool.executeResumeReceipt
                                                            runtime
                                                            ctx
                                                            (ToolHostCodec.argumentsOfRaw raw)

                                                    return
                                                        match receipt with
                                                        | Ok placed ->
                                                            envelopeOk
                                                                [ "name" ==> placed.Name
                                                                  "agentId" ==> placed.AgentId
                                                                  "outcome" ==> placed.Outcome ]
                                                        | Error code -> refusal code
                                            | "devops" ->
                                                match material with
                                                | Error failure ->
                                                    return
                                                        refusalWithReason
                                                            (JsFailure.code failure)
                                                            (JsFailure.reason failure)
                                                | Ok record ->
                                                    let! gitCommonDir = askGitCommonDir workspaceDirectory

                                                    if String.IsNullOrWhiteSpace gitCommonDir then
                                                        return
                                                            refusalWithReason
                                                                "devops-failed"
                                                                "devops failed: git common directory is missing"
                                                    else
                                                        let raw = payloadWithRecord payload record
                                                        let intentText = devopsIntentText payload raw

                                                        let intent =
                                                            match record with
                                                            | Some text -> intentText + "\n\n" + text
                                                            | None -> intentText

                                                        let delegateTo = devopsDelegateOf syncDelegateRuntime ctx

                                                        let! outcome =
                                                            Wanxiangshu.Mission.Planning.PlanTools.executeAsk
                                                                gitCommonDir
                                                                ctx.SessionId
                                                                intent
                                                                delegateTo

                                                        return
                                                            match outcome with
                                                            | Ok answer ->
                                                                envelopeOk
                                                                    [ "kind" ==> answer.Kind
                                                                      "status" ==> answer.Status
                                                                      "view" ==> answer.View ]
                                                            | Error reason ->
                                                                refusalWithReason
                                                                    "devops-failed"
                                                                    (sprintf "devops failed: %s" reason)
                                            | _ -> return refusal "unknown-delegate"
                                        }
                                  Join =
                                    fun () ->
                                        task {
                                            let! receipt = JoinTool.executeReceipt runtime ctx

                                            return
                                                match receipt with
                                                | Ok records ->
                                                    let toObj (record: JoinTool.JoinRecord) =
                                                        createObj
                                                            [ "name" ==> record.Name
                                                              "agentId" ==> record.AgentId
                                                              "role" ==> record.Role
                                                              "status" ==> record.Status
                                                              "view" ==> record.View ]

                                                    envelopeOk
                                                        [ "records"
                                                          ==> (records |> List.map toObj |> Array.ofList)
                                                          "byName"
                                                          ==> createObj
                                                                  [ for record in records ->
                                                                        record.Name ==> box (toObj record) ] ]
                                                | Error code -> refusal code
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
                                  Advisor =
                                    fun intent reportMapping ->
                                        advisorConsult runtime ctx intent reportMapping }

                            JsDelegationBridge.install ctx.SessionId host
                            JsDelegationBridge.installAsk ctx.SessionId (fun question -> runtime.AskUser(ctx, question))

                            task {
                                try
                                    let! outcome = spec.Execute args ctx

                                    // The commit-side fact of a delivery: the live cell
                                    // carries this call's exact fence, so THIS call
                                    // established the delivery and the ledger may
                                    // record it. A cell of another fence, or no cell
                                    // at all, is no delivery of this call.
                                    match ctx.ProviderRunId, ctx.ToolCallId with
                                    | Some providerRun, Some toolCallId ->
                                        match JsDeliveredValues.peekFence ctx.SessionId with
                                        | Some fence
                                            when fence.ProviderRun = providerRun
                                                 && fence.ToolCallId = toolCallId ->
                                            runtime.RegisterEstablishedDelivery(
                                                SessionId.create ctx.SessionId,
                                                providerRun,
                                                toolCallId
                                            )
                                        | _ -> ()
                                    | _ -> ()

                                    return outcome
                                finally
                                    JsDelegationBridge.clear ctx.SessionId
                            } })

        let generatedJsSpecs () =
            [ for role in Roles.all do
                  yield! generatedJsSpecFor role |> Option.toList ]

        // Clean-break: all host delegation and lifecycle verbs (fork, resume,
        // commission, devops, join, horizon, fission, review, deliver, fetch)
        // are retired from host registration and live exclusively as
        // programming-surface members.
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

        let fenceAndCapture journal sessionId snapshotPort owningMessageId owningCallId =
            task {
                let! exchange = readOwningExchange snapshotPort sessionId owningMessageId owningCallId
                return! captureOrPropagate journal sessionId exchange
            }

        let resolveDeliverRetirementCursor
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            : Task<Result<int64, string>> =
            task {
                match deliverPorts runtime, deliverOwnership ctx with
                | Ok(snapshotPort, journal), Ok(owningMessageId, owningCallId) ->
                    return!
                        fenceAndCapture
                            journal
                            (SessionId.create ctx.SessionId)
                            snapshotPort
                            owningMessageId
                            owningCallId
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

        let registerPostS3IfEntered
            (runtime: ToolRuntimeScope)
            (ctx: HostToolContext)
            (outcome: Result<DeliverResult, string>)
            =
            match outcome, ctx.ProviderRunId, ctx.ToolCallId with
            | Ok res, Some runId, Some callId when res.RetirementOutcome = "PostS3" ->
                runtime.RegisterPostS3Interrupt(SessionId.create ctx.SessionId, runId, callId)
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
                | Ok retirementCursor ->
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

                    registerPostS3IfEntered runtime ctx outcome

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
                | Ok retirementCursor ->
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
                  Admission = ToolAdmission.OfficeRole(fun _ role -> role = Role.Plan)
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
                Admission =
                    ToolAdmission.OfficeRole(fun _ role ->
                        OfficeCapability.isAllowed role ToolPermission.Finality
                        || OfficeCapability.isAllowed role ToolPermission.Deliver)
                Execute =
                    fun args ctx ->
                        task {
                            // Algorithm G1: the deliver hard gate.
                            let parkedHandles = JsParkedSites.handlesForSession ctx.SessionId

                            if not (List.isEmpty parkedHandles) then
                                let handles = String.Join(", ", parkedHandles)

                                return
                                    ToolHostCodec.tomlObjectWithInstructions
                                        [ ProviderProse.render
                                              (lang ctx)
                                              Path.DeliverBlockedByParked
                                              (Map [ "handles", handles ]) ]
                                        [ "code", ToolHostCodec.TString Path.DeliverBlockedByParked
                                          "parked_handles", ToolHostCodec.TString handles ]
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

        let baseSpecs =
            [ yield JoinTool.spec runtime

              let horizonContext: HorizonTool.HorizonRuntimeContext =
                  { RuntimeFor = runtime.RuntimeFor
                    LogicalOwnerFor = runtime.LogicalOwnerFor
                    Journal = runtime.Journal
                    EnsureWorkDevOpsBound = runtime.EnsureWorkDevOpsBound }

              yield HorizonTool.spec horizonContext
              yield FissionTool.spec factory runtime
              yield ReviewShell.spec factory runtime
              yield deliverTool
              yield BashHoneypotTool.spec
              yield AssumeTool.spec factory

              yield! casebookToolSpecs
              yield! generatedJsSpecs () ]

        let gateFacts: ToolGateFacts =
            { RoleFor = (fun ctx -> runtime.RoleFor ctx)
              EnsureRoleFor = (fun ctx -> runtime.EnsureRoleFor ctx)
              ManagerFactsFor = (fun sessionId -> runtime.ManagerCapabilityFactsFor sessionId)
              IsRetirementFrozen = (fun sessionId -> runtime.IsRetirementFrozen sessionId)
              IsWaitingForUser = runtime.IsWaitingForUser
              WaitingQuestionFenceFor = runtime.WaitingQuestionFenceFor
              BeginToolExecution = beginToolExecution
              EndToolExecution = endToolExecution
              RecordedDeliverReplay = recordedDeliverReplay }

        let gateExecute (spec: ToolSpec) =
            let managerPermission =
                match ManagerReviewTools.requiredPermissions spec.Name with
                | Some perms -> perms |> Seq.tryHead
                | None -> StaticTools.permissionOfToolName Role.Manager spec.Name

            fun args (ctx: HostToolContext) -> executeGate gateFacts spec managerPermission args ctx

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
