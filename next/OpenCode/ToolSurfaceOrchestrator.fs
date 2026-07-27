namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Fable.Core.JsInterop
open Wanxiangshu.Next.Process
open ToolSurfaceEmit

/// Orchestrator-role routing helpers for the fork/join tool surface.
module ToolSurfaceOrchestrator =

    type HostFactoryDeps =
        { Sessions: ISessionHostPort
          Journal: AgentJournal option
          ModelConfig: ModelResolver.ModelConfig option
          WorkspaceDirectory: string option
          SessionParents: Dictionary<string, string>
          SessionRoles: Dictionary<string, string>
          SessionDirectories: Dictionary<string, string>
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
                          RegisterChildDirectory =
                            fun childId path -> deps.SessionDirectories.[SessionId.value childId] <- path
                          RegisterReviewerTree = fun reviewerId port -> deps.TreePorts.[reviewerId] <- port
                          RepoPath = defaultArg deps.WorkspaceDirectory "."
                          TargetBranch = "" },
                        SessionId.create sid
                    )

                hosts.[sid] <- host
                host)

    /// Builds the fork/verdict tool argument objects and the shared tool
    /// definition builder for the fork/join/verdict tool surface.
    let toolDefBuilders (factory: obj) =
        let forkArgs =
            createObj
                [ "agent", box (stringSchema factory)
                  "prompt", box (optionalStringSchema factory)
                  "signal", box (optionalEnumSchema factory [| PtySignal.TermName; PtySignal.KillName |]) ]

        let verdictArgs =
            createObj [ "verdict", box (enumSchema factory [| "PERFECT"; "REVISE" |]) ]

        let definition desc args execute =
            createObj
                [ "description", box desc
                  "args", box args
                  "execute", uncurriedExecute (box execute) ]

        forkArgs, verdictArgs, definition

    /// Private mailbox runtime for the Executor summarizer: same parent session
    /// (so child sessions are real) and children ARE registered into the shared
    /// sessionRoles/sessionParents maps (the strict-mock lane matcher reads them),
    /// but with no journal linkage. The private Join mailbox (a separate
    /// HostForkRuntime instance) is what stops it stealing the Manager mailbox.
    let executorRuntimeFor
        (gate: obj)
        (executorRuntimes: System.Collections.Generic.Dictionary<string, HostForkRuntime>)
        (mkSid: string -> SessionId)
        (sessionPort: ISessionHostPort)
        (sessionParents: Dictionary<string, string>)
        (sessionRoles: Dictionary<string, string>)
        (modelConfig: ModelResolver.ModelConfig option)
        (ctx: obj)
        : HostForkRuntime =
        let sid =
            if isNull ctx || isNull ctx?sessionID then
                ""
            else
                unbox<string> ctx?sessionID

        lock gate (fun () ->
            match executorRuntimes.TryGetValue sid with
            | true, r -> r
            | false, _ ->
                let r =
                    HostForkRuntime(
                        mkSid sid,
                        sessionPort,
                        ?journal = None,
                        onChildCreated =
                            (fun _ role childId -> registerChild sessionParents sessionRoles sid role childId),
                        ?modelResolver = modelConfig
                    )

                executorRuntimes.[sid] <- r
                r)

    /// Disposes the per-session executor runtime: drops the cached instance and
    /// cancels it (best-effort, fire-and-forget). The next `executorRuntimeFor`
    /// call recreates a fresh runtime, so a cancelled runtime is never reused.
    let disposeExecutorRuntime
        (gate: obj)
        (executorRuntimes: System.Collections.Generic.Dictionary<string, HostForkRuntime>)
        (sessionKey: string)
        : unit =
        lock gate (fun () ->
            match executorRuntimes.TryGetValue sessionKey with
            | true, r ->
                executorRuntimes.Remove sessionKey |> ignore
                // Fire-and-forget: Cancel is async (PTY cleanup + child abort) but
                // disposal must not block the host event path.
                r.Cancel() |> ignore
            | false, _ -> ())
