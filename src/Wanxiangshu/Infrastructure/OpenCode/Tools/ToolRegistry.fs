namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Assembly-only registry: tool behavior lives in one vertical verb module;
/// per-session resources live in ToolRuntimeScope.
type ToolRegistration =
    { Tools: obj
      Runtime: ToolRuntimeScope }

module ToolRegistry =

    /// AGENT-007 role gate, plus request-scoped `blog` (CurrentRequest / InFlight).
    /// sessionId is the tool call's Host session; parkedHost is optional for tests.
    let rolePredicate (specName: string) (parkedHost: IParkedTransformHost option) (sessionId: string) : Role -> bool =
        match specName with
        | "fork" -> fun r -> r = Role.Manager
        | "fork-manager" -> fun r -> r = Role.Orchestrator
        | "fork-pty" -> fun r -> r = Role.DevOps
        | "join" -> fun r -> Roles.isAllowed r ToolPermission.Join
        | "list" -> fun r -> Roles.isAllowed r ToolPermission.List
        | "verdict" -> fun r -> Roles.isAllowed r ToolPermission.Verdict
        | "suicide" -> fun r -> Roles.isAllowed r ToolPermission.Finality
        | "executor" -> fun r -> Roles.isAllowed r ToolPermission.Exec
        | "inspector" -> fun r -> Roles.isAllowed r ToolPermission.Inspector
        | "mv" -> fun r -> Roles.isAllowed r ToolPermission.Move
        | "rm" -> fun r -> Roles.isAllowed r ToolPermission.Remove
        | "coder" -> fun r -> r = Role.DevOps
        | "blog" -> fun r -> r = Role.Blogger && BlogTool.hasLiveCycle parkedHost sessionId
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
                ?eventPort = eventPort
            )

        let baseSpecs =
            [ ForkTool.managerSpec factory runtime
              PtyTool.spec factory runtime
              ForkTool.orchestratorSpec factory runtime
              JoinTool.spec runtime
              ListTool.spec runtime
              VerdictTool.spec factory runtime
              // GLORY-034/036: the Manager's end-of-life tool.
              FinalityTool.spec factory runtime
              ExecutorTool.spec factory runtime
              InspectorTool.spec factory runtime
              CoderTool.spec factory runtime
              // AGENT-016/017/018: Coder-only POSIX mv/rm.
              FileMutationTools.mvSpec factory
              FileMutationTools.rmSpec factory
              // ENFORCER-010: Blogger's tool set is exactly { blog }.
              // parkedHost + CurrentRequest gate request-scoped execute (InFlight).
              BlogTool.spec factory runtime parkedHost ]

        // Role-gated tools: agent permission schema and this execute gate agree on
        // CanonicalRole. blog is request-scoped on top of Role=Blogger: no live
        // CurrentRequest must not complete as a soft tool error (Host step loop).
        let gateExecute (spec: ToolSpec) =
            let original = spec.Execute
            let tString = ToolHostCodec.TString

            let denyRole (role: Role) =
                ToolHostCodec.tomlObject
                    [ "error", tString (sprintf "Tool '%s' is not permitted for role '%A'" spec.Name role) ]

            let stopBlogNoLiveCycle (ctx: HostToolContext) =
                task {
                    Diagnostic.emit "blog-gate" [ "session_id", ctx.SessionId; "result", "no live CurrentRequest" ]

                    if not (String.IsNullOrWhiteSpace ctx.SessionId) then
                        let! _ = runtime.Sessions.AbortSession(SessionId.create ctx.SessionId)
                        ()

                    return raise (InvalidOperationException(BlogTool.NoLiveCycleError))
                }

            fun args (ctx: HostToolContext) ->
                task {
                    let allowed = rolePredicate spec.Name parkedHost ctx.SessionId

                    match runtime.RoleFor ctx with
                    | Some role when allowed role -> return! original args ctx
                    | Some role when spec.Name = "blog" && role = Role.Blogger -> return! stopBlogNoLiveCycle ctx
                    | Some role -> return denyRole role
                    | None ->
                        match! runtime.EnsureRoleFor ctx with
                        | Some role when allowed role -> return! original args ctx
                        | Some role when spec.Name = "blog" && role = Role.Blogger -> return! stopBlogNoLiveCycle ctx
                        | Some role -> return denyRole role
                        | None ->
                            // AGENT-007: an unresolved Role means an empty tool set.
                            // Executing under an unknown role is unauthorised.
                            return
                                ToolHostCodec.tomlObject
                                    [ "error",
                                      tString (
                                          sprintf
                                              "Tool '%s' rejected: no Authority Root fixes this session's role"
                                              spec.Name
                                      ) ]
                }

        let specs =
            baseSpecs |> List.map (fun spec -> { spec with Execute = gateExecute spec })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
