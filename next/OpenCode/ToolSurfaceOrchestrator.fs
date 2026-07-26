namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

/// Orchestrator-role routing helpers for the fork/join tool surface.
module ToolSurfaceOrchestrator =

    type HostFactoryDeps =
        { Sessions: ISessionHostPort
          Journal: AgentJournal option
          ModelConfig: ModelResolver.ModelConfig option
          WorkspaceDirectory: string option
          SessionParents: Dictionary<string, string>
          SessionRoles: Dictionary<string, string>
          TreePorts: Dictionary<string, GitTreePort> }

    let isOrchestratorSession (sessionRoles: Dictionary<string, string>) (sid: string) =
        match sessionRoles.TryGetValue sid with
        | true, role -> role.Equals("orchestrator", StringComparison.OrdinalIgnoreCase)
        | false, _ -> false

    let registerChild
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (parentSid: string)
        (role: AgentRole)
        (childId: SessionId)
        =
        let cid = SessionId.value childId
        sessionParents.[cid] <- parentSid
        sessionRoles.[cid] <- role.ToString().ToLowerInvariant()

    let hostFor
        (deps: HostFactoryDeps)
        (gate: obj)
        (hosts: Dictionary<string, OrchestratorHost>)
        (sid: string)
        : OrchestratorHost =
        lock gate (fun () ->
            match hosts.TryGetValue sid with
            | true, host -> host
            | false, _ ->
                let host =
                    OrchestratorHost(
                        { Sessions = deps.Sessions
                          Journal = deps.Journal
                          ModelConfig = deps.ModelConfig
                          OnChildCreated =
                            fun _ role childId -> registerChild deps.SessionParents deps.SessionRoles sid role childId
                          RegisterReviewerTree = fun reviewerId port -> deps.TreePorts.[reviewerId] <- port
                          RepoPath = defaultArg deps.WorkspaceDirectory "."
                          TargetBranch = "" },
                        SessionId.create sid
                    )

                hosts.[sid] <- host
                host)
