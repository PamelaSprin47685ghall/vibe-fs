namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Review
open Wanxiangshu.Session

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
    let rolePredicate (specName: string) (parkedHost: IParkedTransformHost option) (sessionId: string) : Role -> bool =
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
        (verdictSessions: HashSet<string>)
        (sessionDirectories: Dictionary<string, string>)
        (onRunStarted: (SessionId -> Role -> string option -> unit) option)
        (parentWorkRecordFor: (string -> Task<string option>) option)
        (childWorkRecordFor: (string -> Task<string option>) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        (parkedHost: IParkedTransformHost option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (strengthRuntime: StrengthRuntime option)
        (finalityReviewerTimeoutMs: int option)
        (casebookToolSpecs: ToolSpec list)
        (jsTransactionPersistence: IJsTransactionPersistence option)
        =
        let factory = ToolHostCodec.factory toolModule
        let providerLanguage = ProviderLanguageBinding.readGlobalPreference ()
        let jsProse = JsDescriptionAssets.load providerLanguage

        let runtime =
            new ToolRuntimeScope(
                sessionPort,
                journal,
                gitTreePort,
                workspaceDirectory,
                sessionParents,
                currentPhysicalUserMessage,
                verdictSessions,
                sessionDirectories,
                onRunStarted,
                parentWorkRecordFor,
                childWorkRecordFor,
                snapshot,
                cancelSignals,
                ?eventPort = eventPort,
                ?finalityReviewerTimeoutMs = finalityReviewerTimeoutMs
            )

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
              // ENFORCER-010: Blogger's tool set is exactly { chronicle }.
              // parkedHost + CurrentRequest gate request-scoped execute (InFlight).
              yield ChronicleTool.spec factory runtime parkedHost
              // CASE-009: the conditional fetch / js-bookkeeper tools.
              yield! casebookToolSpecs
              // JS-001/JS-073: the capability-projected js-* tools. The surface
              // is generated from the role matrix (AGENT-007: profile capability
              // set == Roles.permissions), so a role without filesystem
              // capability gets no js-* spec at all.
              for role in RoleDefinitions.all |> List.map (fun d -> d.Role) do
                  match JsToolGenerator.generate (string role) (Roles.permissions role) jsProse with
                  | Some surface ->
                      yield JsToolSpec.create factory surface (defaultArg workspaceDirectory "") jsTransactionPersistence
                  | None -> () ]

        // Role-gated tools: agent permission schema and this execute gate agree on
        // CanonicalRole. chronicle is request-scoped on top of Role=Blogger: no live
        // CurrentRequest must not complete as a soft tool error (Host step loop).
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
                        let! _ = runtime.Sessions.AbortSession(SessionId.create ctx.SessionId)
                        ()

                    return raise (InvalidOperationException(ChronicleTool.NoLiveCycleError))
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

                    if isStrengthReplica then
                        // STRENGTH-004: Host-native read/glob/grep are the entire
                        // replica tool surface. Every plugin-registered tool is
                        // therefore a forged/hidden path and fails closed here.
                        return denied ctx Path.DeniedStrength Map.empty
                    elif isBookkeeper then
                        // Bookkeeper may run js-bookkeeper only. No Role.Bookkeeper.
                        if spec.Name = "js-bookkeeper" then
                            return! original args ctx
                        else
                            return denied ctx Path.DeniedBookkeeperOnly Map.empty
                    elif spec.Name = "js-bookkeeper" then
                        return denied ctx Path.DeniedNoStagedCase Map.empty
                    else
                        match runtime.RoleFor ctx with
                        | Some role when allowed role -> return! original args ctx
                        | Some role when spec.Name = "chronicle" && role = Role.Blogger ->
                            return! stopChronicleNoLiveCycle ctx
                        | Some role -> return denyRole ctx role
                        | None ->
                            match! runtime.EnsureRoleFor ctx with
                            | Some role when allowed role -> return! original args ctx
                            | Some role when spec.Name = "chronicle" && role = Role.Blogger ->
                                return! stopChronicleNoLiveCycle ctx
                            | Some role -> return denyRole ctx role
                            | None ->
                                // AGENT-007: an unresolved Role means an empty tool set.
                                // Executing under an unknown role is unauthorised.
                                return denied ctx Path.DeniedUnestablished Map.empty
                }

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
