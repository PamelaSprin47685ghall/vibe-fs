namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Contract
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Strength

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

        [<Literal>]
        let DeniedStrength = "tool/registry/denied-strength"

        [<Literal>]
        let DeniedUnestablished = "tool/registry/denied-unestablished"

    let private lang (ctx: HostToolContext) =
        let sessionText = ctx.SessionId

        ProviderLanguageBinding.forSessionText sessionText

    let private staticAdmissions (bloggerHost: IBloggerRuntimeHost option) : (string * ToolAdmission) list =
        [ "fork", ForkTool.managerAdmission
          "commission", ForkTool.orchestratorAdmission
          "open-terminal", PtyTool.admission
          "send-terminal", PtyTool.admission
          "read-terminal", PtyTool.admission
          "signal-terminal", PtyTool.admission
          "join", JoinTool.admission
          "horizon", HorizonTool.admission
          "fission", FissionTool.admission
          "judge", JudgeTool.admission
          "suicide", FinalityTool.admission
          "run", ExecutorTool.runAdmission
          "query-shell", ExecutorTool.queryShellAdmission
          "inspect", InspectorTool.admission
          "establish-behavior", CoderTool.behaviorAdmission
          "repair-behavior", CoderTool.behaviorAdmission
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
          "fetch", FetchTool.admission
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

    /// ENF-006: the authority the execute gate resolves for a tool, so a
    /// consumer can tell an office tool from an internal leaf without guessing
    /// from the tool name.
    let tryAdmission (specName: string) (bloggerHost: IBloggerRuntimeHost option) : ToolAdmission option =
        tryAdmissionFor specName bloggerHost

    /// ENF-006: the internal-leaf decision for a session that holds no public
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
        // ENF-006: an internal leaf tool is admitted by attachment, never by a
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
        (gitTreePort: GitTreePort option)
        (workspaceDirectory: string option)
        (sessionParents: Dictionary<string, string>)
        (currentPhysicalUserMessage: string -> string option)
        (verdictSubmissions: HashSet<string>)
        (sessionDirectories: Dictionary<string, string>)
        (onRunStarted: (SessionId -> Role -> string option -> unit) option)
        (parentWorkRecordFor: (string -> Task<string option>) option)
        (childWorkRecordFor: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        (bloggerHost: IBloggerRuntimeHost option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (strengthRuntime: StrengthRuntime option)
        (finalityReviewerTimeoutMs: int option)
        (casebookToolSpecs: ToolSpec list)
        (jsTransactionPersistence: IJsTransactionPersistence option)
        =
        let factory = ToolHostCodec.factory toolModule
        let providerLanguage = ProviderLanguageBinding.readGlobalPreference ()
        let jsProse = JsDescriptionAssets.load providerLanguage

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
                gitTreePort,
                workspaceDirectory,
                sessionParents,
                currentPhysicalUserMessage,
                verdictSubmissions,
                sessionDirectories,
                onRunStarted,
                parentWorkRecordFor,
                childWorkRecordFor,
                snapshot,
                cancelSignals,
                ?eventPort = eventPort,
                ?finalityReviewerTimeoutMs = finalityReviewerTimeoutMs
            )

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
              yield! PtyTool.specs factory runtime
              yield ForkTool.orchestratorSpec factory runtime
              yield JoinTool.spec runtime
              yield HorizonTool.spec runtime
              yield FissionTool.spec factory runtime
              yield JudgeTool.spec factory runtime
              yield FinalityTool.spec factory runtime
              yield ExecutorTool.runSpec factory runtime
              yield ExecutorTool.queryShellSpec factory runtime
              yield InspectorTool.spec factory runtime syncDelegateRuntime
              yield CoderTool.establishSpec factory runtime syncDelegateRuntime
              yield CoderTool.repairSpec factory runtime syncDelegateRuntime
              yield FileMutationTools.mvSpec factory
              yield FileMutationTools.rmSpec factory
              yield BashHoneypotTool.spec
              yield AssumeTool.spec factory
              yield! AttentionTools.specs factory journal
              yield! ConcernTools.specs factory journal
              yield! InstitutionalLearningTools.specs factory journal
              yield ChronicleTool.spec factory runtime bloggerHost
              yield! casebookToolSpecs
              yield! generatedJsSpecs () ]

        // Generic execute gate: every tool declares the authority it is admitted
        // under, and the registry never invents one the session does not hold.
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute

            let denied (ctx: HostToolContext) path (subs: Map<string, string>) =
                ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

            let denyRole (ctx: HostToolContext) (role: Role) =
                denied ctx Path.DeniedRole (Map [ "tool", spec.Name; "role", sprintf "%A" role ])

            let executeKnownRole officeAdmission args (ctx: HostToolContext) (role: Role) =
                task {
                    if officeAdmission ctx role then
                        return! original args ctx
                    else
                        return denyRole ctx role
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
                match strengthRuntime with
                | Some strength when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                    strength.TryFindByReplica(SessionId.create ctx.SessionId) |> Option.isSome
                | _ -> false

            let executeAfterBoundary args (ctx: HostToolContext) =
                task {
                    if isStrengthReplica ctx then
                        // STRENGTH-004: Host-native read/glob/grep are the entire replica surface.
                        return denied ctx Path.DeniedStrength Map.empty
                    else
                        return! executeEstablished args ctx
                }

            fun args (ctx: HostToolContext) ->
                task {
                    match providerToolBoundary ctx with
                    | Error error -> return raise (InvalidOperationException error)
                    | Ok() -> return! executeAfterBoundary args ctx
                }

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
