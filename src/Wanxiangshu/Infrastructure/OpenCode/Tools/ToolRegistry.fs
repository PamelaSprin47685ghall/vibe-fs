namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Persist
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

    /// OpenCode `tool.definition` has no session id. Tool descriptions therefore
    /// freeze to the process ProviderLanguage at registry creation; per-session
    /// localization remains available for system/runtime prose where Host carries
    /// session identity. Tool/argument names remain invariant.
    let private descriptionFor (language: ProviderLanguage) (spec: ToolSpec) =
        match language with
        | ProviderLanguage.English -> spec.Description
        | ProviderLanguage.SimplifiedChinese ->
            match spec.Name with
            | "fork" -> "把一项独立工作托付给另一个受管理的 Agent；返回的是工作已被托付的事实。"
            | "commission" -> "委派一条由 Manager 独立承担并最终协调回共享目标的道路。"
            | "open-terminal" -> "打开一个具名的持续交互 terminal，并在其中启动 command。"
            | "send-terminal" -> "向具名 terminal 发送输入；缺少末尾换行时会自动补上。"
            | "read-terminal" -> "读取具名 terminal 尚未读取的输出。"
            | "signal-terminal" -> "向具名 terminal 发送结构化 process signal。"
            | "join" -> "接收已经抵达或随后抵达的工作后果；仅在真实 dependency 需要时等待。"
            | "horizon" -> "查看当前仍在远处、已经返回或仍存活的工作，用于方向判断而非读取内部状态机。"
            | "fission" -> "请求让同一 Manager identity 获得多个独立 present。当前 MVP 在没有可证明容量时 fail closed，不创建虚假 lane。"
            | "judge" -> "提交 Reviewer 对当前 review barrier 的判断。"
            | "suicide" -> "当 mission 内已经没有有用工作时，寻求结束当前 Manager life。"
            | "run" -> "在受限 deadline、output budget 与 shared-capacity 承诺下运行 command，并返回执行观察。"
            | "query-shell" -> "通过静态 shell 查询建立 repository 中已经存在的事实；不得让项目运行起来。"
            | "inspect" -> "请 Inspector 建立一个 repository 事实，并在普通完成后返回有界 WorkRecord。"
            | "establish-behavior" -> "托付 Coder 写出能够建立目标 behavior 的源码证据；本工具自身不执行测试。"
            | "repair-behavior" -> "托付 Coder 根据既有 runtime evidence 修复 behavior；执行验证仍属于 DevOps。"
            | "mv" -> "移动或重命名文件或目录。"
            | "rm" -> "删除文件或空目录；拒绝递归删除非空目录。"
            | "bash-honeypot" -> "Honeypot：Coder 不得执行 bash；调用只会返回明确拒绝，不会运行任何 command。"
            | "chronicle" -> "记录一个已经发生、值得未来记住的 occurrence，并选择它教会当前 participant 的 Tip。"
            | "fetch" -> "读取一个已经完成并可复用的 Casebook case；不会重新调查世界。"
            | "js-bookkeeper" -> "在 Bookkeeper 的 process-local staging 上原子更新当前 question/answer case。"
            | name when name.StartsWith "js-" -> "在一次可编程调用中组合当前角色获授权的 repository 操作；不会扩大该角色的 capability。"
            | _ -> spec.Description

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
        (parentWorkRecordFor: (string -> string option) option)
        (childWorkRecordFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
        (parkedHost: IParkedTransformHost option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (strengthRuntime: StrengthRuntime option)
        (finalityReviewerTimeoutMs: int option)
        (casebookToolSpecs: ToolSpec list)
        =
        let factory = ToolHostCodec.factory toolModule

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
              yield FissionTool.spec factory
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
                  match JsToolGenerator.generate (string role) (Roles.permissions role) with
                  | Some surface -> yield JsToolSpec.create factory surface (defaultArg workspaceDirectory "") None
                  | None -> () ]

        // Role-gated tools: agent permission schema and this execute gate agree on
        // CanonicalRole. chronicle is request-scoped on top of Role=Blogger: no live
        // CurrentRequest must not complete as a soft tool error (Host step loop).
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute

            let denied message =
                ToolHostCodec.tomlObjectWithInstructions [ message ] []

            let denyRole (role: Role) =
                denied (sprintf "%s is not available to %A." spec.Name role)

            let stopChronicleNoLiveCycle (ctx: HostToolContext) =
                task {
                    Diagnostic.emit "chronicle-gate" [ "session_id", ctx.SessionId; "result", "no live CurrentRequest" ]

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
                        return denied "This tool is not available in this execution context."
                    elif isBookkeeper then
                        // Bookkeeper may run js-bookkeeper only. No Role.Bookkeeper.
                        if spec.Name = "js-bookkeeper" then
                            return! original args ctx
                        else
                            return denied "Only js-bookkeeper is available while reshaping a staged Case."
                    elif spec.Name = "js-bookkeeper" then
                        return denied "There is no staged Case to reshape in this execution context."
                    else
                        match runtime.RoleFor ctx with
                        | Some role when allowed role -> return! original args ctx
                        | Some role when spec.Name = "chronicle" && role = Role.Blogger ->
                            return! stopChronicleNoLiveCycle ctx
                        | Some role -> return denyRole role
                        | None ->
                            match! runtime.EnsureRoleFor ctx with
                            | Some role when allowed role -> return! original args ctx
                            | Some role when spec.Name = "chronicle" && role = Role.Blogger ->
                                return! stopChronicleNoLiveCycle ctx
                            | Some role -> return denyRole role
                            | None ->
                                // AGENT-007: an unresolved Role means an empty tool set.
                                // Executing under an unknown role is unauthorised.
                                return denied "This tool is unavailable until the caller's authority is established."
                }

        let providerLanguage = ProviderLanguageBinding.readGlobalPreference ()

        let specs =
            baseSpecs
            |> List.map (fun spec ->
                { spec with
                    Description = descriptionFor providerLanguage spec
                    Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
