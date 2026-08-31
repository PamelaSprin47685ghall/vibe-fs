namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

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

    /// AGENT-007 role gate, delegates to owner-defined tool admissions.
    /// sessionId is the tool call's Host session; bloggerHost is optional for tests.
    let rolePredicate (specName: string) (bloggerHost: IBloggerRuntimeHost option) (sessionId: string) : Role -> bool =
        let ctx =
            { SessionId = sessionId
              Agent = None
              ToolCallId = None
              ProviderRunId = None
              PromptText = None
              AttachAbort = fun _ -> id }

        match staticAdmissions bloggerHost |> List.tryFind (fun (name, _) -> name = specName) with
        | Some(_, admission) -> admission ctx
        | None when specName.StartsWith "js-" && specName <> "js-bookkeeper" ->
            JsToolSpec.admissionFor (specName.Substring 3) ctx
        | None -> fun _ -> false

    let create
        (toolModule: obj)
        (sessionPort: ISessionHostPort)
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

        // Generic execute gate: delegates tool admission to spec.Admission.
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute

            let denied (ctx: HostToolContext) path (subs: Map<string, string>) =
                ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

            let denyRole (ctx: HostToolContext) (role: Role) =
                denied ctx Path.DeniedRole (Map [ "tool", spec.Name; "role", sprintf "%A" role ])

            let executeKnownRole args (ctx: HostToolContext) (role: Role) =
                task {
                    if spec.Admission ctx role then
                        return! original args ctx
                    else
                        return denyRole ctx role
                }

            let executeAfterEnsure args (ctx: HostToolContext) =
                task {
                    match! runtime.EnsureRoleFor ctx with
                    | Some role -> return! executeKnownRole args ctx role
                    | None -> return denied ctx Path.DeniedUnestablished Map.empty
                }

            let executeEstablished args (ctx: HostToolContext) =
                task {
                    match runtime.RoleFor ctx with
                    | Some role -> return! executeKnownRole args ctx role
                    | None -> return! executeAfterEnsure args ctx
                }

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
