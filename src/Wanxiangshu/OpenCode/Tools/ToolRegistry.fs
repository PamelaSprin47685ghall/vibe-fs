namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Relay.OpenCode
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Participant.Cognition
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Ablation

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

    let private lang (ctx: HostToolContext) =
        let sessionText = ctx.SessionId

        ProviderLanguageBinding.forSessionText sessionText

    let private staticAdmissions (bloggerHost: IBloggerRuntimeHost option) : (string * ToolAdmission) list =
        [ "fork", ForkTool.managerAdmission
          "resume", ForkTool.managerAdmission
          "commission", ForkTool.orchestratorAdmission
          "open-terminal", PtyTool.admission
          "send-terminal", PtyTool.admission
          "read-terminal", PtyTool.admission
          "signal-terminal", PtyTool.admission
          "join", JoinTool.admission
          "horizon", HorizonTool.admission
          "fission", FissionTool.admission
          "review", ReviewTool.admission
          "suicide", SuicideTool.admission
          "run", ExecutorTool.runAdmission
          "mv", FileMutationTools.mvAdmission
          "rm", FileMutationTools.rmAdmission
          "bash-honeypot", BashHoneypotTool.admission
          "assume", AssumeTool.admission
          "enough", AttentionTools.admission
          "abandon", AttentionTools.admission
          "defer", AttentionTools.admission
          "subscribe", ConcernTools.admission
          "publish", ConcernTools.admission
          "celebrate", InstitutionalLearningTools.admission
          "regret", InstitutionalLearningTools.admission
          "chronicle", ChronicleTool.admission bloggerHost
          "fetch", ToolAdmission.OfficeRole(fun _ r -> OfficeCapability.isAllowed r ToolPermission.Fetch)
          "js-bookkeeper", JsBookkeeperTool.admission ]

    let private tryAdmissionFor (specName: string) (bloggerHost: IBloggerRuntimeHost option) : ToolAdmission option =
        match staticAdmissions bloggerHost |> List.tryFind (fun (name, _) -> name = specName) with
        | Some(_, admission) -> Some admission
        | None when specName.StartsWith "js-" && specName <> "js-bookkeeper" ->
            Some(JsToolSpec.admissionFor (specName.Substring 3))
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
    let tryAdmission (specName: string) (bloggerHost: IBloggerRuntimeHost option) : ToolAdmission option =
        tryAdmissionFor specName bloggerHost

    /// capability-enforcement-006: the internal-leaf decision for a session that holds no public
    /// office profile at all. An office tool is never admitted this way.
    let privateAttachmentAdmits
        (specName: string)
        (bloggerHost: IBloggerRuntimeHost option)
        (sessionId: string)
        : bool =
        match tryAdmissionFor specName bloggerHost with
        | Some(ToolAdmission.PrivateAttachment predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.OfficeRole _)
        | None -> false

    /// AGENT-007 role gate, delegates to owner-defined tool admissions.
    /// sessionId is the tool call's Host session; bloggerHost is optional for tests.
    let rolePredicate (specName: string) (bloggerHost: IBloggerRuntimeHost option) (sessionId: string) : Role -> bool =
        // capability-enforcement-006: an internal leaf tool is admitted by attachment, never by a
        // public office, so no public Role may ever see it on this surface.
        match tryAdmissionFor specName bloggerHost with
        | Some(ToolAdmission.OfficeRole predicate) -> predicate (probeContext sessionId)
        | Some(ToolAdmission.PrivateAttachment _)
        | None -> fun _ -> false

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
        (parentWorkRecordFor: (string -> Task<string option>) option)
        (childWorkRecordFor: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (beginToolExecution: string -> unit)
        (endToolExecution: string -> unit)
        (eventPort: IEventObservationPort option)
        (bloggerHost: IBloggerRuntimeHost option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (isReplicaSession: (SessionId -> bool) option)
        (casebookToolSpecs: ToolSpec list)
        (jsTransactionPersistence: IJsTransactionPersistence option)
        (continueManagerLoop: SessionId -> string -> Task<Result<unit, string>>)
        (captureWorktreeSnapshot: WorktreePath -> Result<WorkspaceSnapshotId, string>)
        (childWorkRecordForRun:
            (SessionId -> Wanxiangshu.Context.Trace.XTraceRange -> ProviderRunIdentity -> Task<string option>) option)
        (workRecordCapability: Wanxiangshu.Execution.Delegation.DelegationWorkRecordCapability option)
        =
        let factory = ToolHostCodec.factory toolModule
        let providerLanguage = ProviderLanguageBinding.readGlobalPreference ()

        let jsProse: JsCanonicalDescription.Prose =
            JsDescriptionAssets.load providerLanguage

        let groundingObservation (ctx: HostToolContext) readPaths effectPaths =
            match workspaceDirectory with
            | None -> Task.FromResult(())
            | Some _ when System.String.IsNullOrWhiteSpace ctx.SessionId -> Task.FromResult(())
            | Some root -> RequirementGroundingGate.programObservation journal root ctx.SessionId readPaths effectPaths

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
                parentWorkRecordFor,
                childWorkRecordFor,
                snapshot,
                cancelSignals,
                ?childWorkRecordForRun = childWorkRecordForRun,
                ?workRecordCapability = workRecordCapability,
                continueManagerLoop = continueManagerLoop,
                captureWorktreeSnapshot = captureWorktreeSnapshot,
                ?eventPort = eventPort
            )

        // The canvas is durable, not process memory: a restart recovers the owner's
        // committed canvas from the journal instead of silently restarting at `{}`.
        let cognitiveRuntime = CognitiveRuntime(CognitiveJournalAdapter.port journal)

        // The owner is derived from the verified tool context and the current
        // authority. The tool has no workspace selector, so a caller cannot aim it
        // at another owner's canvas.
        let cognitiveOwnerFor (ctx: HostToolContext) =
            if System.String.IsNullOrWhiteSpace ctx.SessionId then
                None
            else
                Some(CognitiveOwner.create (SessionId.create ctx.SessionId) "")

        let generatedJsSpecs () =
            [ for role in Roles.all do
                  match JsToolGenerator.generate (string role) (OfficeCapability.permissions role) jsProse with
                  | Some surface ->
                      yield
                          JsToolSpec.create
                              factory
                              surface
                              (defaultArg workspaceDirectory "")
                              jsTransactionPersistence
                              (Some groundingObservation)
                  | None -> () ]

        let baseSpecs =
            [ yield ForkTool.managerSpec factory runtime
              yield ForkTool.resumeSpec factory runtime
              let ptyContext: PtyTool.PtyRuntimeContext =
                  { IsDevOps = fun ctx -> runtime.IsRole(ctx, Role.DevOps)
                    ManagedAgentFor = runtime.ManagedAgentFor
                    PtyCapabilityFor = runtime.PtyCapabilityFor
                    DirectoryFor = runtime.DirectoryFor
                    WorkspaceDirectory = runtime.WorkspaceDirectory }

              yield! PtyTool.specs factory ptyContext
              yield ForkTool.orchestratorSpec factory runtime
              yield JoinTool.spec runtime

              let horizonContext: HorizonTool.HorizonRuntimeContext =
                  { RuntimeFor = runtime.RuntimeFor
                    LogicalOwnerFor = runtime.LogicalOwnerFor
                    Journal = runtime.Journal
                    EnsureRoadDevOpsBound = runtime.EnsureRoadDevOpsBound }

              yield HorizonTool.spec horizonContext
              yield FissionTool.spec factory runtime
              yield ReviewTool.spec factory runtime
              yield SuicideTool.spec factory runtime
              yield ExecutorTool.runSpec factory runtime
              yield FileMutationTools.mvSpec factory
              yield FileMutationTools.rmSpec factory
              yield BashHoneypotTool.spec
              yield AssumeTool.spec factory cognitiveRuntime cognitiveOwnerFor
              yield! AttentionTools.specs factory (journal |> Option.map AgentJournalPortAdapter.forAttention)
              yield! ConcernTools.specs factory (journal |> Option.map AgentJournalPortAdapter.forConcern)

              yield!
                  InstitutionalLearningTools.specs
                      factory
                      (journal |> Option.map AgentJournalPortAdapter.forInstitutionalLearning)

              yield
                  ChronicleTool.spec
                      factory
                      (fun (sessionId, reason) -> runtime.TerminateSession(sessionId, reason))
                      bloggerHost

              yield ManagerReadTools.readManagerSpec factory runtime groundingObservation
              yield ManagerReadTools.globManagerSpec factory runtime groundingObservation
              yield ManagerReadTools.grepManagerSpec factory runtime groundingObservation
              yield! casebookToolSpecs
              yield! generatedJsSpecs () ]

        // Generic execute gate: every tool declares the authority it is admitted
        // under, and the registry never invents one the session does not hold.
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute

            let managerPermission =
                match ManagerReviewTools.requiredPermissions spec.Name with
                | Some perms -> perms |> Seq.tryHead
                | None ->
                    match spec.Name with
                    | "fork" -> Some ToolPermission.Fork
                    | "resume" -> Some ToolPermission.Resume
                    | "join" -> Some ToolPermission.Join
                    | "horizon" -> Some ToolPermission.Horizon
                    | "fission" -> Some ToolPermission.Fission
                    | "review" -> Some ToolPermission.ReviewAssessment
                    | "suicide" -> Some ToolPermission.Finality
                    | _ -> None

            let denied (ctx: HostToolContext) path (subs: Map<string, string>) =
                ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

            let denyRole (ctx: HostToolContext) (role: Role) =
                let path =
                    if spec.Name = "fission" then
                        Path.DeniedFission
                    else
                        Path.DeniedRole

                denied ctx path (Map [ "tool", spec.Name; "role", sprintf "%A" role ])

            // The current capability decides; the denial stays action-focused
            // and never echoes internal loop state.
            let denyTaskState (ctx: HostToolContext) =
                denied ctx Path.DeniedTaskState (Map [ "tool", spec.Name ])

            let executeManager args (ctx: HostToolContext) =
                task {
                    let facts = runtime.ManagerCapabilityFactsFor ctx.SessionId
                    let frozen = runtime.IsRetirementFrozen ctx.SessionId

                    match managerPermission with
                    | Some permission when
                        frozen
                        && permission <> ToolPermission.Join
                        && permission <> ToolPermission.Finality
                        ->
                        return denyTaskState ctx
                    | Some permission when not (OfficeCapability.isAllowedForManagerFacts facts permission) ->
                        return denyTaskState ctx
                    | _ -> return! original args ctx
                }

            let executeKnownRole officeAdmission args (ctx: HostToolContext) (role: Role) =
                task {
                    if not (officeAdmission ctx role) then
                        return denyRole ctx role
                    elif role <> Role.Manager then
                        return! original args ctx
                    else
                        return! executeManager args ctx
                }

            let executeAfterEnsure officeAdmission args (ctx: HostToolContext) =
                task {
                    match! runtime.EnsureRoleFor ctx with
                    | Some role -> return! executeKnownRole officeAdmission args ctx role
                    | None -> return denied ctx Path.DeniedUnestablished Map.empty
                }

            let executeOffice officeAdmission args (ctx: HostToolContext) =
                task {
                    match runtime.RoleFor ctx with
                    | Some role -> return! executeKnownRole officeAdmission args ctx role
                    | None -> return! executeAfterEnsure officeAdmission args ctx
                }

            // The attachment IS the authority. Resolving a public office Role here
            // would deny the Bookkeeper its own exact tool, because a HostInternal
            // prompt deliberately installs no public authority profile.
            let executePrivateAttachment attachmentAdmission args (ctx: HostToolContext) =
                task {
                    if attachmentAdmission ctx then
                        return! original args ctx
                    else
                        return denied ctx Path.DeniedUnestablished Map.empty
                }

            let executeEstablished args (ctx: HostToolContext) =
                match spec.Admission with
                | ToolAdmission.OfficeRole officeAdmission -> executeOffice officeAdmission args ctx
                | ToolAdmission.PrivateAttachment attachmentAdmission ->
                    executePrivateAttachment attachmentAdmission args ctx

            let providerToolBoundary (ctx: HostToolContext) =
                if String.IsNullOrWhiteSpace ctx.SessionId then
                    Ok()
                else
                    SessionExecutionBinding.endProviderStepAtToolBoundary
                        (SessionId.create ctx.SessionId)
                        ctx.ProviderRunId

            let isStrengthReplica (ctx: HostToolContext) =
                match isReplicaSession with
                | Some replicaPred when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                    replicaPred (SessionId.create ctx.SessionId)
                | _ -> false

            let executeAfterBoundary args (ctx: HostToolContext) =
                task {
                    if AblationGate.toolDenied (AblationGate.registry ()) spec.Name then
                        return denied ctx Path.DeniedAblation (Map [ "tool", spec.Name ])
                    elif isStrengthReplica ctx then
                        // STRENGTH-004: Host-native read/glob/grep are the entire replica surface.
                        return denied ctx Path.DeniedStrength Map.empty
                    else
                        return! executeEstablished args ctx
                }

            let executeTrackedSession args (ctx: HostToolContext) =
                task {
                    beginToolExecution ctx.SessionId

                    try
                        return! executeAfterBoundary args ctx
                    finally
                        endToolExecution ctx.SessionId
                }

            let executeTracked args (ctx: HostToolContext) =
                if String.IsNullOrWhiteSpace ctx.SessionId then
                    executeAfterBoundary args ctx
                else
                    executeTrackedSession args ctx

            fun args (ctx: HostToolContext) ->
                task {
                    match providerToolBoundary ctx with
                    | Error error -> return raise (InvalidOperationException error)
                    | Ok() -> return! executeTracked args ctx
                }

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
