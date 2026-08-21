namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
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
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
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
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
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
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
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
        let DeniedBookkeeperOnly = "tool/registry/denied-bookkeeper-only"

        [<Literal>]
        let DeniedNoStagedCase = "tool/registry/denied-no-staged-case"

        [<Literal>]
        let DeniedUnestablished = "tool/registry/denied-unestablished"

    let private lang (ctx: HostToolContext) =
        if String.IsNullOrWhiteSpace ctx.SessionId then
            ProviderLanguageBinding.readGlobalPreference ()
        else
            ProviderLanguageBinding.ensureRoot (SessionId.create ctx.SessionId)

    /// AGENT-007 role gate, plus request-scoped `chronicle` (CurrentRequest / InFlight).
    /// sessionId is the tool call's Host session; parkedHost is optional for tests.
    let rolePredicate (specName: string) (parkedHost: IBloggerRuntimeHost option) (sessionId: string) : Role -> bool =
        match specName with
        | "fork" -> fun r -> r = Role.Manager
        | "commission" -> fun r -> r = Role.Orchestrator
        | "open-terminal"
        | "send-terminal"
        | "read-terminal"
        | "signal-terminal" -> fun r -> r = Role.DevOps
        | "join" -> fun r -> Roles.isAllowed r ToolPermission.Join
        | "horizon" -> fun r -> Roles.isAllowed r ToolPermission.Horizon
        | "fission" -> fun r -> Roles.isAllowed r ToolPermission.Fission
        | "judge" -> fun r -> Roles.isAllowed r ToolPermission.Judge
        | "suicide" -> fun r -> Roles.isAllowed r ToolPermission.Finality
        | "run" -> fun r -> r = Role.DevOps
        | "query-shell" -> fun r -> r = Role.Inspector
        | "inspect" -> fun r -> Roles.isAllowed r ToolPermission.Inspect
        | "mv" -> fun r -> Roles.isAllowed r ToolPermission.Move
        | "rm" -> fun r -> Roles.isAllowed r ToolPermission.Remove
        | "bash-honeypot" -> fun r -> Roles.isAllowed r ToolPermission.BashHoneypot
        | "establish-behavior"
        | "repair-behavior" -> fun r -> r = Role.DevOps
        | "chronicle" -> fun r -> r = Role.Blogger && ChronicleTool.hasLiveCycle parkedHost sessionId
        | "assume" -> fun r -> r <> Role.Blogger && r <> Role.Distiller
        | "enough"
        | "abandon"
        | "defer"
        | "subscribe"
        | "publish"
        | "celebrate"
        | "regret" -> fun r -> r <> Role.Blogger && r <> Role.Distiller
        // CASE-009: fetch is the next-session Casebook read. Inspector/Coder
        // consume reusable Q/A; Bookkeeper is js-bookkeeper only (gateExecute).
        | "fetch" -> fun r -> r = Role.Inspector || r = Role.Coder
        // JS-001: the js-* gate — the invoked name must be this role's own
        // generated name AND the role must actually hold a filesystem
        // capability (four-layer exactness, forged names fail closed).
        | name when name.StartsWith "js-" && name <> "js-bookkeeper" ->
            let roleName = name.Substring 3

            let fsPermissions =
                set
                    [ ToolPermission.Read
                      ToolPermission.Write
                      ToolPermission.Edit
                      ToolPermission.Glob
                      ToolPermission.Grep ]

            fun r ->
                (string r).ToLowerInvariant() = roleName
                && not (Set.isEmpty (Set.intersect (Roles.permissions r) fsPermissions))
        | _ -> fun _ -> false

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
        (parkedHost: IBloggerRuntimeHost option)
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
            [ for role in RoleDefinitions.all |> List.map (fun d -> d.Role) do
                  match JsToolGenerator.generate (string role) (Roles.permissions role) jsProse with
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
              // GLORY-034/036: the Manager's end-of-life tool.
              yield FinalityTool.spec factory runtime
              yield ExecutorTool.runSpec factory runtime
              yield ExecutorTool.queryShellSpec factory runtime
              yield InspectorTool.spec factory runtime syncDelegateRuntime
              yield CoderTool.establishSpec factory runtime syncDelegateRuntime
              yield CoderTool.repairSpec factory runtime syncDelegateRuntime
              // AGENT-016/017/018: Coder-only POSIX mv/rm.
              yield FileMutationTools.mvSpec factory
              yield FileMutationTools.rmSpec factory
              // Coder-only bash honeypot: visible denial, never a shell.
              yield BashHoneypotTool.spec
              // Cognitive commitment point: no authority or persistence. Kept
              // out of Blogger/Distiller by the ordinary role gate.
              yield AssumeTool.spec factory
              yield! AttentionTools.specs factory journal
              yield! ConcernTools.specs factory journal
              yield! InstitutionalLearningTools.specs factory journal
              // ENFORCER-010: Blogger's tool set is exactly { chronicle }.
              // parkedHost + CurrentRequest gate request-scoped execute (InFlight).
              yield ChronicleTool.spec factory runtime parkedHost
              // CASE-009: the conditional fetch / js-bookkeeper tools.
              yield! casebookToolSpecs
              // JS-001/JS-073: the capability-projected js-* tools. The surface
              // is generated from the role matrix (AGENT-007: profile capability
              // set == Roles.permissions), so a role without filesystem
              // capability gets no js-* spec at all.
              yield! generatedJsSpecs () ]

        // Role-gated tools: agent permission schema and this execute gate agree on
        // CanonicalRole. chronicle is request-scoped on top of Role=Blogger.
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute

            let denied (ctx: HostToolContext) path (subs: Map<string, string>) =
                ToolHostCodec.tomlObjectWithInstructions [ ProviderProse.render (lang ctx) path subs ] []

            let denyRole (ctx: HostToolContext) (role: Role) =
                denied ctx Path.DeniedRole (Map [ "tool", spec.Name; "role", sprintf "%A" role ])

            let stopChronicleNoLiveCycle (ctx: HostToolContext) =
                task {
                    Diagnostic.emit
                        "chronicle-gate"
                        [ "session_id", ctx.SessionId; "result", ChronicleTool.NoLiveCycleError ]

                    if not (String.IsNullOrWhiteSpace ctx.SessionId) then
                        let! _ = runtime.TerminateSession(ctx.SessionId, ChronicleTool.NoLiveCycleError)

                        ()

                    return raise (InvalidOperationException(ChronicleTool.NoLiveCycleError))
                }

            let executeKnownRole args (ctx: HostToolContext) allowed role =
                task {
                    match allowed role, spec.Name = "chronicle" && role = Role.Blogger with
                    | true, _ -> return! original args ctx
                    | _, true -> return! stopChronicleNoLiveCycle ctx
                    | _ -> return denyRole ctx role
                }

            let executeAfterEnsure args (ctx: HostToolContext) allowed =
                task {
                    match! runtime.EnsureRoleFor ctx with
                    | Some role -> return! executeKnownRole args ctx allowed role
                    | None -> return denied ctx Path.DeniedUnestablished Map.empty
                }

            let executeEstablished args (ctx: HostToolContext) allowed =
                task {
                    match runtime.RoleFor ctx with
                    | Some role -> return! executeKnownRole args ctx allowed role
                    | None -> return! executeAfterEnsure args ctx allowed
                }

            let executeNonBookkeeper args (ctx: HostToolContext) allowed =
                task {
                    if spec.Name = "js-bookkeeper" then
                        return denied ctx Path.DeniedNoStagedCase Map.empty
                    else
                        return! executeEstablished args ctx allowed
                }

            let executeBookkeeper args (ctx: HostToolContext) =
                task {
                    if spec.Name = "js-bookkeeper" then
                        return! original args ctx
                    else
                        return denied ctx Path.DeniedBookkeeperOnly Map.empty
                }

            fun args (ctx: HostToolContext) ->
                task {
                    let allowed = rolePredicate spec.Name parkedHost ctx.SessionId

                    let isStrengthReplica =
                        match strengthRuntime with
                        | Some strength when not (String.IsNullOrWhiteSpace ctx.SessionId) ->
                            strength.TryFindByReplica(SessionId.create ctx.SessionId) |> Option.isSome
                        | _ -> false

                    let isBookkeeper =
                        not (String.IsNullOrWhiteSpace ctx.SessionId)
                        && BookkeeperRuntime.isAttached ctx.SessionId

                    match isStrengthReplica, isBookkeeper with
                    | true, _ ->
                        // STRENGTH-004: Host-native read/glob/grep are the entire replica surface.
                        return denied ctx Path.DeniedStrength Map.empty
                    | false, true -> return! executeBookkeeper args ctx
                    | false, false -> return! executeNonBookkeeper args ctx allowed
                }

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
