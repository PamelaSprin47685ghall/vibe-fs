namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open Thoth.Json
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

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
        (sessionRoles: Dictionary<string, string>)
        (currentPhysicalUserMessage: string -> string option)
        (verdictSessions: HashSet<string>)
        (sessionDirectories: Dictionary<string, string>)
        (onRunStarted: (SessionId -> AgentRole -> string option -> unit) option)
        (backgroundFor: (string -> string option) option)
        (snapshot: ISessionSnapshotPort option)
        (cancelSignals: (SessionId seq -> unit) option)
        =
        let factory = ToolHostCodec.factory toolModule

        let runtime =
            new ToolRuntimeScope(
                sessionPort,
                journal,
                gitTreePort,
                workspaceDirectory,
                sessionParents,
                sessionRoles,
                currentPhysicalUserMessage,
                verdictSessions,
                sessionDirectories,
                onRunStarted,
                backgroundFor,
                snapshot,
                cancelSignals
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
              CoderTool.spec factory runtime ]

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
            | _ -> fun _ -> false

        let gateExecute spec allowed =
            let original = spec.Execute

            fun args ctx ->
                task {
                    match runtime.RoleFor ctx with
                    | Some role when allowed role -> return! original args ctx
                    | Some role ->
                        return
                            ToolHostCodec.jsonObject
                                [ "error",
                                  Encode.string (
                                      sprintf "Tool '%s' is not permitted for role '%A'" spec.Name role
                                  ) ]
                    | None ->
                        // In test/unresolved contexts, allow the read-only inspector
                        // tool (broadly permitted across Coder, Meditator, Reviewer,
                        // and DevOps) rather than rejecting it outright.
                        match spec.Name with
                        | "inspector" -> return! original args ctx
                        | _ ->
                            return
                                ToolHostCodec.jsonObject
                                    [ "error",
                                      Encode.string (
                                          sprintf "Tool '%s' rejected: session role could not be determined" spec.Name
                                      ) ]
                }

        let specs =
            baseSpecs
            |> List.map (fun spec -> { spec with Execute = gateExecute spec (rolePredicate spec) })

        { Tools = ToolHostCodec.registry factory specs
          Runtime = runtime }
