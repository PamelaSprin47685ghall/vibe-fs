namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Assembly-only registry: tool behavior lives in one vertical verb module;
/// per-session resources live in ToolRuntimeScope.
type ToolRegistration =
    { Tools: obj
      Runtime: ToolRuntimeScope }

module ToolRegistry =

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
        (onRunStarted: (SessionId -> AgentRole -> string option -> unit) option)
        (parentWorkRecordFor: (string -> string option) option)
        (childWorkRecordFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        (eventPort: IEventObservationPort option)
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
              ExecutorTool.spec factory runtime
              InspectorTool.spec factory runtime
              CoderTool.spec factory runtime
              // ENFORCER-010: Blogger's tool set is exactly { blog }.
              BlogTool.spec factory runtime ]

        let rolePredicate spec =
            match spec.Name with
            | "fork" -> fun r -> r = Role.Manager
            | "fork-manager" -> fun r -> r = Role.Orchestrator
            | "fork-pty" -> fun r -> r = Role.DevOps
            | "join" -> fun r -> Roles.isAllowed r ToolPermission.Join
            | "list" -> fun r -> Roles.isAllowed r ToolPermission.List
            | "verdict" -> fun r -> Roles.isAllowed r ToolPermission.Verdict
            | "executor" -> fun r -> Roles.isAllowed r ToolPermission.Exec
            | "inspector" -> fun r -> Roles.isAllowed r ToolPermission.Inspector
            | "coder" -> fun r -> r = Role.DevOps
            | "blog" -> fun r -> r = Role.Blogger
            | _ -> fun _ -> false

        // AGENT-007 layer two. Both layers read the same CanonicalRole, so a tool
        // the schema admitted is exactly a tool the gate admits.
        let gateExecute spec allowed =
            let original = spec.Execute
            let tString = ToolHostCodec.TString

            fun args ctx ->
                task {
                    match runtime.RoleFor ctx with
                    | Some role when allowed role -> return! original args ctx
                    | Some role ->
                        return
                            ToolHostCodec.tomlObject
                                [ "error", tString (sprintf "Tool '%s' is not permitted for role '%A'" spec.Name role) ]
                    | None ->
                        match! runtime.EnsureRoleFor ctx with
                        | Some role when allowed role -> return! original args ctx
                        | Some role ->
                            return
                                ToolHostCodec.tomlObject
                                    [ "error",
                                      tString (sprintf "Tool '%s' is not permitted for role '%A'" spec.Name role) ]
                        | None ->
                            // AGENT-007: an unresolved Role means an empty tool set. The
                            // previous version exempted `inspector` here on the grounds
                            // that it is read-only and broadly permitted — the exemption
                            // the clause names explicitly. Read-only or not, executing
                            // under an unknown role is an unauthorised execution.
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
            baseSpecs
            |> List.map (fun spec ->
                { spec with
                    Execute = gateExecute spec (rolePredicate spec) })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
